using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        Task<RefreshToken> GenerateRefreshTokenAsync(string userId);
        Task<RefreshToken?> ValidateRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token);
    }
}
