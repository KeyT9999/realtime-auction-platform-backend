using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth;

public class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = null!;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = null!;

    [Required]
    [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
    public string ConfirmPassword { get; set; } = null!;
}
