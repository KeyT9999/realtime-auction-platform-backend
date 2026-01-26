using System.ComponentModel.DataAnnotations;

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
    }
}
