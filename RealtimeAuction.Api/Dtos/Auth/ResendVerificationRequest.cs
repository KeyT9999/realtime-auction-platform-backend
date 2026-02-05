using System.ComponentModel.DataAnnotations;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Auth;

public class ResendVerificationRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    // Optional: để resend cùng phương thức đã chọn
    public VerificationMethod? VerificationMethod { get; set; }
}
