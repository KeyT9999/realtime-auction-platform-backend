using System.ComponentModel.DataAnnotations;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Auth
{
    public class RegisterRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = null!;

        [Required]
        public string FullName { get; set; } = null!;

        // Default to Link to keep existing registration flow compatible.
        public VerificationMethod VerificationMethod { get; set; } = VerificationMethod.Link;

        public string? CaptchaToken { get; set; }
    }
}
