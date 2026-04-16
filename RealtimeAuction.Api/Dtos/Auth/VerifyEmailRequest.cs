// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho VerifyEmailRequest.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth;

public class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = null!;
}
