// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho ForgotPasswordRequest.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    public string CaptchaToken { get; set; } = null!;
}
