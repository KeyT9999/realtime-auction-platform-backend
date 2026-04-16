// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho RefreshTokenRequest.
namespace RealtimeAuction.Api.Dtos.Auth
{
    public class RefreshTokenRequest
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
    }
}
