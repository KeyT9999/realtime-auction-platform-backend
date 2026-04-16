// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho GoogleLoginRequest.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auth
{
    public class GoogleLoginRequest
    {
        [Required]
        public string IdToken { get; set; } = null!;
    }
}
