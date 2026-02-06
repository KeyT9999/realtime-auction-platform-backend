using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Auth;
using RealtimeAuction.Api.Services;
using System.Security.Authentication;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IHostEnvironment _env;

        public AuthController(IAuthService authService, IHostEnvironment env)
        {
            _authService = authService;
            _env = env;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var response = await _authService.RegisterAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);

                // Set refresh token vào HttpOnly cookie
                SetRefreshTokenCookie(response.RefreshToken);

                // Chỉ trả access token trong body
                return Ok(new
                {
                    accessToken = response.AccessToken,
                    id = response.Id,
                    email = response.Email,
                    fullName = response.FullName,
                    role = response.Role
                });
            }
            catch (AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var response = await _authService.GoogleLoginAsync(request);

                // Set refresh token vào HttpOnly cookie
                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    accessToken = response.AccessToken,
                    id = response.Id,
                    email = response.Email,
                    fullName = response.FullName,
                    role = response.Role
                });
            }
            catch (AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            try
            {
                // Ưu tiên lấy refresh token từ cookie
                var cookieRefreshToken = Request.Cookies["refreshToken"];
                var refreshToken = cookieRefreshToken ?? request.RefreshToken;

                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    return Unauthorized(new { message = "Refresh token is missing" });
                }

                var response = await _authService.RefreshTokenAsync(new RefreshTokenRequest
                {
                    RefreshToken = refreshToken
                });

                // Rotate refresh token trong cookie
                SetRefreshTokenCookie(response.RefreshToken);

                return Ok(new
                {
                    accessToken = response.AccessToken,
                    id = response.Id,
                    email = response.Email,
                    fullName = response.FullName,
                    role = response.Role
                });
            }
            catch (AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                await _authService.ForgotPasswordAsync(request);
                // Always return success to prevent email enumeration
                return Ok(new { message = "If the email exists, a password reset link has been sent." });
            }
            catch (Exception)
            {
                // Still return success to prevent email enumeration
                return Ok(new { message = "If the email exists, a password reset link has been sent." });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(new { message = "Password has been reset successfully." });
            }
            catch (AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("reset-password-otp")]
        public async Task<IActionResult> ResetPasswordWithOtp([FromBody] ResetPasswordWithOtpRequest request)
        {
            try
            {
                await _authService.ResetPasswordWithOtpAsync(request);
                return Ok(new { message = "Mật khẩu đã được đặt lại thành công." });
            }
            catch (AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("resend-password-reset-otp")]
        public async Task<IActionResult> ResendPasswordResetOtp([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                await _authService.ResendPasswordResetOtpAsync(request.Email);
                return Ok(new { message = "Mã OTP mới đã được gửi đến email của bạn." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
        {
            try
            {
                await _authService.VerifyEmailAsync(request);
                return Ok(new { message = "Email verified successfully." });
            }
            catch (AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                await _authService.VerifyOtpAsync(request);
                return Ok(new { message = "Email verified successfully." });
            }
            catch (AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request)
        {
            try
            {
                await _authService.ResendVerificationAsync(request.Email, request.VerificationMethod);
                return Ok(new { message = "Verification email sent successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _authService.ChangePasswordAsync(userId, request);
                return Ok(new { message = "Password changed successfully." });
            }
            catch (System.Security.Authentication.AuthenticationException ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (!string.IsNullOrWhiteSpace(refreshToken))
                {
                    await _authService.RevokeRefreshTokenAsync(refreshToken);
                }

                DeleteRefreshTokenCookie();

                return Ok(new { message = "Logged out successfully." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private CookieOptions BuildCookieOptions(bool persistent = true)
        {
            // Dev: cross-port -> SameSite=None; Prod: Lax
            var sameSite = _env.IsDevelopment() ? SameSiteMode.None : SameSiteMode.Lax;

            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // yêu cầu HTTPS để dùng SameSite=None
                SameSite = sameSite,
                Expires = persistent ? DateTimeOffset.UtcNow.AddDays(7) : null,
                Path = "/"
            };
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return;
            }

            var options = BuildCookieOptions();
            Response.Cookies.Append("refreshToken", refreshToken, options);
        }

        private void DeleteRefreshTokenCookie()
        {
            var options = BuildCookieOptions();
            options.Expires = DateTimeOffset.UtcNow.AddDays(-1);
            Response.Cookies.Delete("refreshToken", options);
        }
    }
}
