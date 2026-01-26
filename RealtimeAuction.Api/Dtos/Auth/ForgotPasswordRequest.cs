using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
}
