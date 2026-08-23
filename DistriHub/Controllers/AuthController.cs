using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;
using DistriHub.Repository;
using DistriHub.Models;

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

            // generate refresh token and persist it with expiry
            var refreshToken = GenerateRefreshToken();
            var refreshExpiry = DateTime.UtcNow.AddDays(7); // refresh token valid for 7 days
            await _repository.SetRefreshTokenAsync(request.Source!.Trim(), refreshToken, refreshExpiry);

            return Ok(new TokenResponse { AccessToken = token, RefreshToken = refreshToken, ExpiresAt = tokenDescriptor.ValidTo });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.RefreshToken))
                return BadRequest(new { error = "Invalid request" });

            var stored = await _repository.GetRefreshTokenAsync(request.Source.Trim());
            if (stored.RefreshToken == null || stored.Expiry == null)
                return Unauthorized(new { error = "Invalid refresh token" });

            if (!string.Equals(stored.RefreshToken, request.RefreshToken.Trim(), StringComparison.Ordinal))
                return Unauthorized(new { error = "Invalid refresh token" });

            if (stored.Expiry.Value < DateTime.UtcNow)
                return Unauthorized(new { error = "Refresh token expired" });

            // Create new access token
            var key = _configuration.GetValue<string>("Jwt:Key");
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("Configuration error: Jwt:Key is missing.");

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

            var securityKey = new SymmetricSecurityKey(keyBytesForJwt);
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: credentials
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);

            // rotate refresh token
            var newRefreshToken = GenerateRefreshToken();
            var refreshExpiry = DateTime.UtcNow.AddDays(7);
            await _repository.SetRefreshTokenAsync(request.Source.Trim(), newRefreshToken, refreshExpiry);

            return Ok(new TokenResponse { AccessToken = accessToken, RefreshToken = newRefreshToken, ExpiresAt = tokenDescriptor.ValidTo });
        }

        // helper function to generate secure random refresh token
        private static string GenerateRefreshToken()
        {
            var randomNumber = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        // Access validation is performed against the UserDetails table via IRepository.
    }

}
