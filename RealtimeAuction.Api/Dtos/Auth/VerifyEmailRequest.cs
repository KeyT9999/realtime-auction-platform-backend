using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth;

public class VerifyEmailRequest
{
    [Required]
    public string Token { get; set; } = null!;
}
