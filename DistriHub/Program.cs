using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using DistriHub.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Enable runtime compilation in Development so .cshtml changes are picked up without rebuilding
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
}
else
{
    builder.Services.AddControllersWithViews();
}

// Register repository for database operations
builder.Services.AddScoped<DistriHub.Repository.IRepository, DistriHub.Repository.Repository>();
// Register serial service
builder.Services.AddScoped<DistriHub.Services.Interfaces.ISerialService, DistriHub.Services.SerialService>();

// JWT configuration
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key");
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("Configuration error: Jwt:Key is missing. Provide a Base64-encoded 32-byte (or larger) key in configuration.");

byte[] jwtKeyBytes;
try { jwtKeyBytes = Convert.FromBase64String(jwtKey); }
catch (FormatException)
{
    // Jwt:Key is not base64 - treat as raw UTF8 string
    jwtKeyBytes = Encoding.UTF8.GetBytes(jwtKey);
}
if (jwtKeyBytes.Length < 32)
    throw new InvalidOperationException($"Configuration error: Jwt:Key is too short ({jwtKeyBytes.Length} bytes). It must be at least 32 bytes (256 bits). Use a Base64-encoded 32-byte key or a UTF-8 secret >= 32 chars.");

var jwtIssuer = jwtSection.GetValue<string>("Issuer") ?? "DistriHub";
var jwtAudience = jwtSection.GetValue<string>("Audience") ?? "DistriHubUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(jwtKeyBytes)
        };
    });

// Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DistriHub API", Version = "v1" });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter 'Bearer {token}'",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    c.AddSecurityDefinition("Bearer", securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { securityScheme, new string[] { } }
    });
});

builder.Services.AddAuthorization();

// Add simple file-based logger (writes warnings and errors to Logs/*.txt)
builder.Logging.AddFileLogger(options =>
{
    options.LogDirectory = System.IO.Path.Combine(builder.Environment.ContentRootPath, "Logs");
    options.FileNamePrefix = "distrihub";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
// Custom JWT middleware to allow token validation and attaching user to HttpContext
app.UseMiddleware<DistriHub.Middleware.JwtMiddleware>();
app.UseAuthorization();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DistriHub API V1"));

// Map attribute-routed controller actions (e.g. [HttpGet("Home/DownloadTemplate")])
app.MapControllers();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Simple JwtMiddleware implementation in same file to avoid adding new files during quick iteration
namespace DistriHub.Middleware
{
    // Minimal middleware class added here so Program can reference it without a separate file
    public class JwtMiddleware
    {
        private readonly global::Microsoft.AspNetCore.Http.RequestDelegate _next;
        private readonly global::Microsoft.Extensions.Configuration.IConfiguration _config;

        public JwtMiddleware(global::Microsoft.AspNetCore.Http.RequestDelegate next, global::Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _next = next;
            _config = config;
        }

        public async global::System.Threading.Tasks.Task InvokeAsync(global::Microsoft.AspNetCore.Http.HttpContext context)
        {
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                await _next(context);
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", System.StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                await _next(context);
                return;
            }

            try
            {
                var key = _config.GetValue<string>("Jwt:Key");
                if (string.IsNullOrWhiteSpace(key))
                    throw new InvalidOperationException("Configuration error: Jwt:Key is missing. Provide a Base64-encoded 32-byte (or larger) key in configuration.");
                var issuer = _config.GetValue<string>("Jwt:Issuer");
                var audience = _config.GetValue<string>("Jwt:Audience");
                byte[] _middlewareKeyBytes;
                try { _middlewareKeyBytes = Convert.FromBase64String(key); }
                catch (FormatException)
                {
                    // Key not base64 - fall back to UTF8 bytes
                    _middlewareKeyBytes = System.Text.Encoding.UTF8.GetBytes(key);
                }

                if (_middlewareKeyBytes.Length < 32)
                    throw new InvalidOperationException($"Configuration error: Jwt:Key is too short ({_middlewareKeyBytes.Length} bytes). It must be at least 32 bytes (256 bits). Provide a Base64-encoded 32-byte key or a UTF-8 secret >= 32 chars.");

                var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var validationParameters = new global::Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new global::Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(_middlewareKeyBytes),
                    ClockSkew = System.TimeSpan.FromMinutes(1)
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                if (principal?.Identity != null && principal.Identity.IsAuthenticated)
                    context.User = principal;
            }
            catch (Exception)
            {
                // ignore invalid token (explicitly catch Exception to avoid swallowing non-exception errors)
            }

            await _next(context);
        }
    }
}
