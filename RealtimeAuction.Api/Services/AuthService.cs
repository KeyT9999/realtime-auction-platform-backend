using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RealtimeAuction.Api.Dtos.Auth;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Models;
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
            _tokenService = tokenService;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
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

            // Send verification email
            try
            {
                await SendVerificationEmailAsync(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
                // Don't fail registration if email fails
            }

            return GenerateAuthResponse(user);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new AuthenticationException("Invalid credentials");
            }

            if (user.IsLocked)
            {
                throw new AuthenticationException($"Account is locked. Reason: {user.LockedReason ?? "No reason provided"}");
            }

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
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _users.InsertOneAsync(user);
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

            // Generate secure token
            var tokenBytes = RandomNumberGenerator.GetBytes(32);
            var token = Convert.ToBase64String(tokenBytes).Replace("+", "-").Replace("/", "_").Replace("=", "");

            // Create reset token with 30-minute expiration
            var resetToken = new PasswordResetToken
            {
                Token = token,
                UserId = user.Id!,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false,
                CreatedAt = DateTime.UtcNow
            };

            await _passwordResetTokens.InsertOneAsync(resetToken);

            // Build reset URL
            var frontendUrl = _configuration["FrontendUrl"] ?? "http://localhost:5173";
            var resetUrl = $"{frontendUrl}/reset-password?token={token}";

            // Send email
            try
            {
                await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, token, resetUrl);
                _logger.LogInformation("Password reset email sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", user.Email);
                // Don't throw - token is already created, user can request again
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

        public async Task ResendVerificationAsync(string email)
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

            await SendVerificationEmailAsync(user);
            _logger.LogInformation("Verification email resent to {Email}", user.Email);
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
