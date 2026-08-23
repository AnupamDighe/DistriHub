using System;

namespace DistriHub.Models
{
    public class TokenResponse
    {
        public string? AccessToken { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime ExpiresAt { get; set; }
    }

    public class RefreshRequest
    {
        public string? Source { get; set; }
        public string? RefreshToken { get; set; }
    }

    public class AuthRequest
    {
        public string? Source { get; set; }
        public string? AccessCode { get; set; }
    }
}
