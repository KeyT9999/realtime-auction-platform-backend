using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos;

public class GoogleLoginDto
{
    [Required(ErrorMessage = "Google ID token is required")]
    public string IdToken { get; set; } = string.Empty;
}
