using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RealtimeAuction.Api.Dtos.Auth;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using System.Security.Authentication;
using System.Security.Cryptography;
using BCrypt.Net;

namespace RealtimeAuction.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly IMongoCollection<User> _users;
        private readonly IMongoCollection<PasswordResetToken> _passwordResetTokens;
        private readonly IMongoCollection<EmailVerificationToken> _emailVerificationTokens;
        private readonly IMongoCollection<OtpToken> _otpTokens;
        private readonly IMongoCollection<PasswordResetOtp> _passwordResetOtps;
        private readonly ITokenService _tokenService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            IMongoDatabase database, 
            ITokenService tokenService, 
            IEmailService emailService,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _users = database.GetCollection<User>("Users");
            _passwordResetTokens = database.GetCollection<PasswordResetToken>("PasswordResetTokens");
            _emailVerificationTokens = database.GetCollection<EmailVerificationToken>("EmailVerificationTokens");
            _otpTokens = database.GetCollection<OtpToken>("OtpTokens");
            _passwordResetOtps = database.GetCollection<PasswordResetOtp>("PasswordResetOtps");
            _tokenService = tokenService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "AuthService.cs:42", message = "RegisterAsync called", data = new { email = request.Email, verificationMethod = request.VerificationMethod.ToString() }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            var existingUser = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                throw new Exception("Email already exists");
            }

            // Validate password strength
            var passwordValidation = PasswordValidator.ValidatePasswordStrength(request.Password);
            if (!passwordValidation.IsValid)
            {
                throw new Exception(passwordValidation.ErrorMessage);
            }

            var user = new User
            {
                Email = request.Email,
                FullName = request.FullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "User",
                IsEmailVerified = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _users.InsertOneAsync(user);

            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "A", location = "AuthService.cs:68", message = "User created", data = new { userId = user.Id, email = user.Email, isEmailVerified = user.IsEmailVerified }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            // Send verification email dựa trên phương thức được chọn
            bool emailSent = true;
            string? emailErrorMessage = null;
            
            try
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "AuthService.cs:73", message = "About to send verification email", data = new { verificationMethod = request.VerificationMethod.ToString() }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log

                if (request.VerificationMethod == VerificationMethod.Otp)
                {
                    await SendOtpEmailAsync(user);
                }
                else
                {
                    await SendVerificationEmailAsync(user);
                }

                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "AuthService.cs:81", message = "Verification email sent successfully", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
            }
            catch (Exception ex)
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "AuthService.cs:84", message = "Failed to send verification email", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
                // Don't fail registration if email fails, but notify user
                emailSent = false;
                emailErrorMessage = "Đăng ký thành công nhưng không thể gửi email xác thực. Vui lòng liên hệ admin hoặc thử gửi lại sau.";
            }

            // Không trả tokens nếu chưa verify email - user phải verify trước khi login
            // Trả về response không có tokens
            var response = new AuthResponse
            {
                Id = user.Id!,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role!,
                AccessToken = string.Empty, // Không có token
                RefreshToken = string.Empty, // Không có token
                EmailSent = emailSent,
                Message = emailErrorMessage
            };

            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "C", location = "AuthService.cs:98", message = "RegisterAsync returning response", data = new { hasAccessToken = !string.IsNullOrEmpty(response.AccessToken), hasRefreshToken = !string.IsNullOrEmpty(response.RefreshToken) }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            return response;
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "AuthService.cs:101", message = "LoginAsync called", data = new { email = request.Email }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            var user = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new AuthenticationException("Invalid credentials");
            }

            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "AuthService.cs:107", message = "User found and password verified", data = new { userId = user.Id, isEmailVerified = user.IsEmailVerified, isLocked = user.IsLocked }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            if (user.IsLocked)
            {
                throw new AuthenticationException($"Account is locked. Reason: {user.LockedReason ?? "No reason provided"}");
            }

            if (!user.IsEmailVerified)
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "AuthService.cs:115", message = "Email not verified - blocking login", data = new { userId = user.Id }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                throw new AuthenticationException("Email is not verified. Please check your inbox and verify your email.");
            }

            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "AuthService.cs:119", message = "Login successful - generating tokens", data = new { userId = user.Id }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            return GenerateAuthResponse(user);
        }

        public async Task<AuthResponse> RefreshTokenAsync(RefreshTokenRequest request)
        {
            var refreshToken = await _tokenService.ValidateRefreshTokenAsync(request.RefreshToken);
            if (refreshToken == null)
            {
                throw new AuthenticationException("Invalid or expired refresh token");
            }

            var user = await _users.Find(u => u.Id == refreshToken.UserId).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new AuthenticationException("User not found");
            }

            // Revoke old refresh token (Security best practice: Rotate tokens)
            await _tokenService.RevokeRefreshTokenAsync(request.RefreshToken);

            return GenerateAuthResponse(user);
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return;
            }

            await _tokenService.RevokeRefreshTokenAsync(refreshToken);
        }

        public async Task<AuthResponse> GoogleLoginAsync(GoogleLoginRequest request)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken);

                var user = await _users.Find(u => u.Email == payload.Email).FirstOrDefaultAsync();
                if (user == null)
                {
                    user = new User
                    {
                        Email = payload.Email,
                        FullName = payload.Name,
                        PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()), // Random password for Google users
                        Role = "User",
                        IsEmailVerified = true,
                        EmailVerifiedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _users.InsertOneAsync(user);
                }
                else if (!user.IsEmailVerified)
                {
                    // Nếu user đã tồn tại nhưng chưa verify, login bằng Google sẽ auto verify
                    user.IsEmailVerified = true;
                    user.EmailVerifiedAt = DateTime.UtcNow;
                    user.UpdatedAt = DateTime.UtcNow;
                    await _users.ReplaceOneAsync(u => u.Id == user.Id, user);
                }

                return GenerateAuthResponse(user);

            }
            catch (InvalidJwtException ex)
            {
                throw new AuthenticationException("Invalid Google Token", ex);
            }
        }

        public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            // Security: Don't reveal if email exists
            var user = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
            if (user == null)
            {
                // Return success even if user doesn't exist to prevent email enumeration
                _logger.LogWarning("Password reset requested for non-existent email: {Email}", request.Email);
                return;
            }

            // Tạo mã OTP 6 chữ số
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // Hash OTP bằng BCrypt
            var otpCodeHash = BCrypt.Net.BCrypt.HashPassword(otpCode);

            // Tạo OTP token với thời hạn 10 phút
            var passwordResetOtp = new PasswordResetOtp
            {
                OtpCodeHash = otpCodeHash,
                Email = user.Email,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _passwordResetOtps.InsertOneAsync(passwordResetOtp);

            // Send email với OTP
            try
            {
                await _emailService.SendPasswordResetOtpEmailAsync(user.Email, user.FullName, otpCode);
                _logger.LogInformation("Password reset OTP sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset OTP to {Email}", user.Email);
                throw; // Throw để frontend biết email không gửi được
            }
        }

        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            // Find token
            var resetToken = await _passwordResetTokens
                .Find(t => t.Token == request.Token && !t.IsUsed)
                .FirstOrDefaultAsync();

            if (resetToken == null || resetToken.ExpiresAt <= DateTime.UtcNow)
            {
                throw new AuthenticationException("Invalid or expired reset token");
            }

            // Find user
            var user = await _users.Find(u => u.Id == resetToken.UserId).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new AuthenticationException("User not found");
            }

            // Validate password strength
            var passwordValidation = PasswordValidator.ValidatePasswordStrength(request.NewPassword);
            if (!passwordValidation.IsValid)
            {
                throw new Exception(passwordValidation.ErrorMessage);
            }

            // Update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

            // Mark token as used
            var update = Builders<PasswordResetToken>.Update.Set(t => t.IsUsed, true);
            await _passwordResetTokens.UpdateOneAsync(t => t.Id == resetToken.Id, update);

            _logger.LogInformation("Password reset successful for user {UserId}", user.Id);
        }

        public async Task ResetPasswordWithOtpAsync(ResetPasswordWithOtpRequest request)
        {
            // Tìm user theo email
            var user = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new AuthenticationException("Email không tồn tại trong hệ thống");
            }

            // Tìm OTP token chưa dùng, chưa hết hạn, sắp xếp theo CreatedAt DESC
            var otpRecord = await _passwordResetOtps
                .Find(t => t.Email == request.Email && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .SortByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpRecord == null)
            {
                throw new AuthenticationException("Mã OTP không hợp lệ hoặc đã hết hạn");
            }

            // Kiểm tra số lần nhập sai (tối đa 5 lần)
            if (otpRecord.Attempts >= 5)
            {
                throw new AuthenticationException("Mã OTP đã bị khóa do nhập sai quá nhiều lần. Vui lòng yêu cầu mã mới.");
            }

            // Verify OTP bằng BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.OtpCode, otpRecord.OtpCodeHash))
            {
                // Tăng số lần nhập sai
                var update = Builders<PasswordResetOtp>.Update.Inc(t => t.Attempts, 1);
                await _passwordResetOtps.UpdateOneAsync(t => t.Id == otpRecord.Id, update);
                
                throw new AuthenticationException("Mã OTP không chính xác");
            }

            // Validate password strength
            var passwordValidation = PasswordValidator.ValidatePasswordStrength(request.NewPassword);
            if (!passwordValidation.IsValid)
            {
                throw new Exception(passwordValidation.ErrorMessage);
            }

            // Đổi mật khẩu
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

            // Đánh dấu OTP đã dùng
            var markUsed = Builders<PasswordResetOtp>.Update.Set(t => t.IsUsed, true);
            await _passwordResetOtps.UpdateOneAsync(t => t.Id == otpRecord.Id, markUsed);

            _logger.LogInformation("Password reset via OTP successful for user {UserId}", user.Id);
        }

        public async Task ResendPasswordResetOtpAsync(string email)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null)
            {
                // Don't reveal if email exists
                return;
            }

            // Tạo mã OTP 6 chữ số mới
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // Hash OTP bằng BCrypt
            var otpCodeHash = BCrypt.Net.BCrypt.HashPassword(otpCode);

            // Tạo OTP token mới với thời hạn 10 phút
            var passwordResetOtp = new PasswordResetOtp
            {
                OtpCodeHash = otpCodeHash,
                Email = user.Email,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _passwordResetOtps.InsertOneAsync(passwordResetOtp);

            // Send email với OTP mới
            await _emailService.SendPasswordResetOtpEmailAsync(user.Email, user.FullName, otpCode);
            _logger.LogInformation("Password reset OTP resent to {Email}", user.Email);
        }

        public async Task VerifyEmailAsync(VerifyEmailRequest request)
        {
            // Find token
            var verificationToken = await _emailVerificationTokens
                .Find(t => t.Token == request.Token && !t.IsUsed)
                .FirstOrDefaultAsync();

            if (verificationToken == null || verificationToken.ExpiresAt <= DateTime.UtcNow)
            {
                throw new AuthenticationException("Invalid or expired verification token");
            }

            // Find user
            var user = await _users.Find(u => u.Id == verificationToken.UserId).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new AuthenticationException("User not found");
            }

            // Update user verification status
            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

            // Mark token as used
            var update = Builders<EmailVerificationToken>.Update.Set(t => t.IsUsed, true);
            await _emailVerificationTokens.UpdateOneAsync(t => t.Id == verificationToken.Id, update);

            _logger.LogInformation("Email verified for user {UserId}", user.Id);
        }

        public async Task ResendVerificationAsync(string email, VerificationMethod? method = null)
        {
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user == null)
            {
                // Don't reveal if email exists
                return;
            }

            if (user.IsEmailVerified)
            {
                throw new Exception("Email is already verified");
            }

            // Nếu không chỉ định method, mặc định dùng Link
            var verificationMethod = method ?? VerificationMethod.Link;

            if (verificationMethod == VerificationMethod.Otp)
            {
                await SendOtpEmailAsync(user);
            }
            else
            {
                await SendVerificationEmailAsync(user);
            }

            _logger.LogInformation("Verification email resent to {Email} using {Method}", user.Email, verificationMethod);
        }

        public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
        {
            // Find user
            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new AuthenticationException("User not found");
            }

            // Verify old password
            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                throw new AuthenticationException("Current password is incorrect");
            }

            // Validate password strength
            var passwordValidation = PasswordValidator.ValidatePasswordStrength(request.NewPassword);
            if (!passwordValidation.IsValid)
            {
                throw new Exception(passwordValidation.ErrorMessage);
            }

            // Update password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;
            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

            _logger.LogInformation("Password changed for user {UserId}", user.Id);
        }

        public async Task VerifyOtpAsync(VerifyOtpRequest request)
        {
            // Tìm user theo email
            var user = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
            if (user == null)
            {
                throw new AuthenticationException("User not found");
            }

            // Tìm OTP token chưa dùng, chưa hết hạn, sắp xếp theo CreatedAt DESC
            var otpToken = await _otpTokens
                .Find(t => t.UserId == user.Id && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow)
                .SortByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (otpToken == null)
            {
                throw new AuthenticationException("Invalid or expired OTP code");
            }

            // Kiểm tra số lần nhập sai (tối đa 5 lần)
            if (otpToken.Attempts >= 5)
            {
                throw new AuthenticationException("OTP code has been locked due to too many failed attempts");
            }

            // Verify OTP bằng BCrypt
            if (!BCrypt.Net.BCrypt.Verify(request.OtpCode, otpToken.OtpCodeHash))
            {
                // Tăng số lần nhập sai
                var update = Builders<OtpToken>.Update.Inc(t => t.Attempts, 1);
                await _otpTokens.UpdateOneAsync(t => t.Id == otpToken.Id, update);
                
                throw new AuthenticationException("Invalid OTP code");
            }

            // Xác minh thành công - cập nhật user
            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

            // Đánh dấu OTP đã dùng
            var markUsed = Builders<OtpToken>.Update.Set(t => t.IsUsed, true);
            await _otpTokens.UpdateOneAsync(t => t.Id == otpToken.Id, markUsed);

            _logger.LogInformation("Email verified via OTP for user {UserId}", user.Id);
        }

        private async Task SendOtpEmailAsync(User user)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "AuthService.cs:404", message = "SendOtpEmailAsync called", data = new { userId = user.Id, email = user.Email }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            // Tạo mã OTP 6 chữ số
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "AuthService.cs:410", message = "OTP code generated", data = new { otpCode = otpCode }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            // Hash OTP bằng BCrypt
            var otpCodeHash = BCrypt.Net.BCrypt.HashPassword(otpCode);

            // Tạo OTP token với thời hạn 10 phút
            var otpToken = new OtpToken
            {
                OtpCodeHash = otpCodeHash,
                UserId = user.Id!,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                IsUsed = false,
                Attempts = 0,
                CreatedAt = DateTime.UtcNow
            };

            await _otpTokens.InsertOneAsync(otpToken);

            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "AuthService.cs:427", message = "OTP token saved, about to send email", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            // Gửi email
            await _emailService.SendOtpEmailAsync(user.Email, user.FullName, otpCode);
            
            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "AuthService.cs:431", message = "SendOtpEmailAsync completed", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            _logger.LogInformation("OTP sent to {Email}", user.Email);
        }

        private async Task SendVerificationEmailAsync(User user)
        {
            // Generate secure token
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

            // Create verification token with 24-hour expiration
            var verificationToken = new EmailVerificationToken
            {
                Token = token,
                UserId = user.Id!,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            await _emailVerificationTokens.InsertOneAsync(verificationToken);

            // Build verification URL
            var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:5173";
            var verificationUrl = $"{frontendUrl}/verify-email?token={token}";

            // Send email
            await _emailService.SendVerificationEmailAsync(user.Email, user.FullName, token, verificationUrl);
        }

        private AuthResponse GenerateAuthResponse(User user)
        {
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken(user.Id!);

            return new AuthResponse
            {
                Id = user.Id!,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role!,
                AccessToken = accessToken,
                RefreshToken = refreshToken.Token
            };
        }
    }
}
