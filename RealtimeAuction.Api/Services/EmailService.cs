using Microsoft.Extensions.Options;
using RealtimeAuction.Api.Settings;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace RealtimeAuction.Api.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> emailSettings, ILogger<EmailService> logger)
    {
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlContent)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_emailSettings.ApiKey))
            {
                _logger.LogWarning("SendGrid API key is not configured. Email will not be sent.");
                return;
            }

            var client = new SendGridClient(_emailSettings.ApiKey);
            var from = new EmailAddress(_emailSettings.FromEmail, _emailSettings.FromName);
            var to = new EmailAddress(toEmail, toName);
            var msg = MailHelper.CreateSingleEmail(from, to, subject, null, htmlContent);
            
            var response = await client.SendEmailAsync(msg);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Email sent successfully to {Email}", toEmail);
            }
            else
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogError("Failed to send email to {Email}. Status: {Status}, Body: {Body}", 
                    toEmail, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Email}", toEmail);
            throw;
        }
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetToken, string resetUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "PasswordResetEmail.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{ResetUrl}}", resetUrl);
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{Token}}", resetToken);
        
        await SendEmailAsync(toEmail, toName, "Reset Your Password - Realtime Auction Platform", htmlContent);
    }

    public async Task SendVerificationEmailAsync(string toEmail, string toName, string verificationToken, string verificationUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "VerificationEmail.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{VerificationUrl}}", verificationUrl);
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{Token}}", verificationToken);
        
        await SendEmailAsync(toEmail, toName, "Verify Your Email - Realtime Auction Platform", htmlContent);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "WelcomeEmail.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        
        await SendEmailAsync(toEmail, toName, "Welcome to Realtime Auction Platform", htmlContent);
    }
}
