using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth;

public class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;
}
