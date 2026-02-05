using Microsoft.Extensions.Options;
using RealtimeAuction.Api.Settings;
using SendGrid;
using SendGrid.Helpers.Mail;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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
            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:22", message = "SendEmailAsync called", data = new { toEmail = toEmail, useSmtp = _emailSettings.UseSmtp, smtpHost = _emailSettings.SmtpHost, hasSmtpUsername = !string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername), hasSmtpPassword = !string.IsNullOrWhiteSpace(_emailSettings.SmtpPassword) }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log

            _logger.LogInformation("Attempting to send email. UseSmtp: {UseSmtp}, SmtpHost: {SmtpHost}, SmtpUsername: {SmtpUsername}", 
                _emailSettings.UseSmtp, 
                string.IsNullOrWhiteSpace(_emailSettings.SmtpHost) ? "NOT SET" : _emailSettings.SmtpHost,
                string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername) ? "NOT SET" : "SET");
            
            if (_emailSettings.UseSmtp)
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:30", message = "Using SMTP to send email", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                await SendEmailViaSmtpAsync(toEmail, toName, subject, htmlContent);
            }
            else
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:35", message = "Using SendGrid to send email", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                await SendEmailViaSendGridAsync(toEmail, toName, subject, htmlContent);
            }

            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:40", message = "SendEmailAsync completed successfully", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log
        }
        catch (Exception ex)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:43", message = "SendEmailAsync exception", data = new { error = ex.Message, stackTrace = ex.StackTrace }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log
            _logger.LogError(ex, "Error sending email to {Email}", toEmail);
            throw;
        }
    }

    private async Task SendEmailViaSmtpAsync(string toEmail, string toName, string subject, string htmlContent)
    {
        // #region agent log
        try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:47", message = "SendEmailViaSmtpAsync called", data = new { smtpHost = _emailSettings.SmtpHost, smtpPort = _emailSettings.SmtpPort, hasUsername = !string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername), hasPassword = !string.IsNullOrWhiteSpace(_emailSettings.SmtpPassword) }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
        // #endregion agent log

        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost) || 
            string.IsNullOrWhiteSpace(_emailSettings.SmtpUsername) ||
            string.IsNullOrWhiteSpace(_emailSettings.SmtpPassword))
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:52", message = "SMTP settings not configured", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
            // #endregion agent log
            _logger.LogWarning("SMTP settings are not configured. Email will not be sent.");
            return;
        }

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_emailSettings.FromName, _emailSettings.FromEmail));
        message.To.Add(new MailboxAddress(toName, toEmail));
        
        if (!string.IsNullOrWhiteSpace(_emailSettings.ReplyTo))
        {
            message.ReplyTo.Add(new MailboxAddress(_emailSettings.ReplyTo, _emailSettings.ReplyTo));
        }
        
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlContent
        };
        message.Body = bodyBuilder.ToMessageBody();

        // #region agent log
        try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:74", message = "About to connect to SMTP", data = new { smtpHost = _emailSettings.SmtpHost, smtpPort = _emailSettings.SmtpPort }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
        // #endregion agent log

        using (var client = new SmtpClient())
        {
            await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        // #region agent log
        try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "B", location = "EmailService.cs:82", message = "Email sent successfully via SMTP", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
        // #endregion agent log

        _logger.LogInformation("Email sent successfully via SMTP to {Email}", toEmail);
    }

    private async Task SendEmailViaSendGridAsync(string toEmail, string toName, string subject, string htmlContent)
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
            _logger.LogInformation("Email sent successfully via SendGrid to {Email}", toEmail);
        }
        else
        {
            var body = await response.Body.ReadAsStringAsync();
            _logger.LogError("Failed to send email to {Email}. Status: {Status}, Body: {Body}", 
                toEmail, response.StatusCode, body);
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

    public async Task SendOtpEmailAsync(string toEmail, string toName, string otpCode)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "OtpEmail.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{OtpCode}}", otpCode);
        
        await SendEmailAsync(toEmail, toName, "Your Verification Code - Realtime Auction Platform", htmlContent);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "WelcomeEmail.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        
        await SendEmailAsync(toEmail, toName, "Welcome to Realtime Auction Platform", htmlContent);
    }
}
