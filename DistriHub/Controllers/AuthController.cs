using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using DistriHub.Repository;

namespace DistriHub.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly IRepository _repository;

        public AuthController(IConfiguration configuration, IRepository repository)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        [HttpPost("token")]
        public async Task<IActionResult> Token([FromBody] AuthRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.AccessCode))
            {
                return BadRequest(new { error = "Invalid request" });
            }

            var stored = await _repository.GetPasswordByUsernameAsync(request.Source.Trim());
            if (stored == null || !string.Equals(stored, request.AccessCode.Trim(), StringComparison.Ordinal))
            {
                return Unauthorized(new { error = "Invalid credentials" });
            }

            var key = _configuration.GetValue<string>("Jwt:Key");
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Configuration error: Jwt:Key is missing. Provide a Base64-encoded 32-byte (or larger) key in configuration.");
            }

            var issuer = _configuration.GetValue<string>("Jwt:Issuer") ?? "DistriHub";
            var audience = _configuration.GetValue<string>("Jwt:Audience") ?? "DistriHubUsers";
            var expireMinutes = _configuration.GetValue<int?>("Jwt:ExpireMinutes") ?? 1440;

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, request.Source),
                new Claim("source", request.Source)
            };

            byte[] keyBytesForJwt;
            try
            {
                keyBytesForJwt = Convert.FromBase64String(key);
            }
            catch (FormatException)
            {
                keyBytesForJwt = Encoding.UTF8.GetBytes(key);
            }

            if (keyBytesForJwt.Length < 32)
            {
                throw new InvalidOperationException($"Configuration error: Jwt:Key is too short ({keyBytesForJwt.Length} bytes). It must be at least 32 bytes (256 bits). Use a Base64-encoded 32-byte key or a UTF-8 secret >= 32 chars.");
            }

            var securityKey = new SymmetricSecurityKey(keyBytesForJwt);
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: credentials
            );

            var token = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

            return Ok(new { token });
        }

        // Access validation is performed against the UserDetails table via IRepository.
    }

    public class AuthRequest
    {
        public string? Source { get; set; }
        public string? AccessCode { get; set; }
    }
}
