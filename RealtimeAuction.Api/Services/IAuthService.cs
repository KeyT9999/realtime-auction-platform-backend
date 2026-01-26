using RealtimeAuction.Api.Dtos.Auth;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request);
        Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request);
        Task ForgotPasswordAsync(ForgotPasswordRequest request);
        Task ResetPasswordAsync(ResetPasswordRequest request);
        Task VerifyEmailAsync(VerifyEmailRequest request);
        Task ResendVerificationAsync(string email);
        Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
    }
}
