using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Driver;
using RealtimeAuction.Api.Dtos.Auth;
using RealtimeAuction.Api.Models;
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
        private readonly ICaptchaVerificationService _captchaVerificationService;
        private readonly IHostEnvironment _env;

        public AuthController(
            IAuthService authService,
            ICaptchaVerificationService captchaVerificationService,
            IHostEnvironment env)
        {
            _authService = authService;
            _captchaVerificationService = captchaVerificationService;
            _env = env;
        }

        [HttpPost("register")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var captchaError = await VerifyCaptchaOrRejectAsync(request.CaptchaToken, "register");
                if (captchaError != null)
                {
                    return captchaError;
                }

                var response = await _authService.RegisterAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var captchaError = await VerifyCaptchaOrRejectAsync(request.CaptchaToken, "login");
                if (captchaError != null)
                {
                    return captchaError;
                }

                var response = await _authService.LoginAsync(request);
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

        [HttpPost("google-login")]
        [EnableRateLimiting("auth")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
        {
            try
            {
                var response = await _authService.GoogleLoginAsync(request);
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
        [EnableRateLimiting("auth-strict")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                var captchaError = await VerifyCaptchaOrRejectAsync(request.CaptchaToken, "forgot_password");
                if (captchaError != null)
                {
                    return captchaError;
                }

                await _authService.ForgotPasswordAsync(request);
                return Ok(new { message = "If the email exists, a password reset link has been sent." });
            }
            catch (Exception)
            {
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
                return Ok(new { message = "Mat khau da duoc dat lai thanh cong." });
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
        [EnableRateLimiting("auth-strict")]
        public async Task<IActionResult> ResendPasswordResetOtp([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                await _authService.ResendPasswordResetOtpAsync(request.Email);
                return Ok(new { message = "Ma OTP moi da duoc gui den email cua ban." });
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
        [EnableRateLimiting("auth-strict")]
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
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User not authenticated" });
                }

                await _authService.ChangePasswordAsync(userId, request);
                return Ok(new { message = "Password changed successfully." });
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

        [HttpPost("dev-seed")]
        public async Task<IActionResult> DevSeed([FromServices] IMongoDatabase db)
        {
            if (!_env.IsDevelopment())
            {
                return NotFound();
            }

            var users = db.GetCollection<User>("Users");
            var results = new List<object>();

            var seeds = new[]
            {
                new { Email = "user@test.com", Password = "Test@1234", FullName = "Test User", Role = "User" },
                new { Email = "admin@test.com", Password = "Admin@1234", FullName = "Admin User", Role = "Admin" }
            };

            foreach (var seed in seeds)
            {
                var existing = await users.Find(u => u.Email == seed.Email).FirstOrDefaultAsync();
                if (existing != null)
                {
                    var update = Builders<User>.Update
                        .Set(u => u.IsEmailVerified, true)
                        .Set(u => u.EmailVerifiedAt, DateTime.UtcNow)
                        .Set(u => u.Role, seed.Role)
                        .Set(u => u.UpdatedAt, DateTime.UtcNow);

                    await users.UpdateOneAsync(u => u.Email == seed.Email, update);
                    results.Add(new { seed.Email, seed.Role, status = "updated" });
                }
                else
                {
                    var user = new User
                    {
                        Email = seed.Email,
                        FullName = seed.FullName,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(seed.Password),
                        Role = seed.Role,
                        IsEmailVerified = true,
                        EmailVerifiedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        AvailableBalance = 10_000_000
                    };

                    await users.InsertOneAsync(user);
                    results.Add(new { seed.Email, seed.Role, status = "created" });
                }
            }

            return Ok(new { message = "Seed completed", accounts = results });
        }

        private CookieOptions BuildCookieOptions(bool persistent = true)
        {
            var isHttps = !_env.IsDevelopment()
                || Request.IsHttps
                || string.Equals(Request.Headers["X-Forwarded-Proto"], "https", StringComparison.OrdinalIgnoreCase);

            return new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
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

            Response.Cookies.Append("refreshToken", refreshToken, BuildCookieOptions());
        }

        private void DeleteRefreshTokenCookie()
        {
            var options = BuildCookieOptions();
            options.Expires = DateTimeOffset.UtcNow.AddDays(-1);
            Response.Cookies.Delete("refreshToken", options);
        }

        private async Task<IActionResult?> VerifyCaptchaOrRejectAsync(string? captchaToken, string action)
        {
            var result = await _captchaVerificationService.VerifyAsync(
                captchaToken,
                action,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted);

            if (result.Success)
            {
                return null;
            }

            return StatusCode(result.StatusCode, new { message = result.ErrorMessage });
        }
    }
}
