// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho VerifyOtpRequest.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth;

public class VerifyOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string OtpCode { get; set; } = null!;
}
