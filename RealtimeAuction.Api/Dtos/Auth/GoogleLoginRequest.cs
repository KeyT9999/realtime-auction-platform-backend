using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth
{
    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; } = null!;
    }
}
