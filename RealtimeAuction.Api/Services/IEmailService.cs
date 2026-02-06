namespace RealtimeAuction.Api.Services;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string toName, string subject, string htmlContent);
    Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, string resetUrl);
    Task SendVerificationEmailAsync(string toEmail, string toName, string verificationToken, string verificationUrl);
    Task SendOtpEmailAsync(string toEmail, string toName, string otpCode);
    Task SendPasswordResetOtpEmailAsync(string toEmail, string toName, string otpCode);
    Task SendWelcomeEmailAsync(string toEmail, string toName);
}
