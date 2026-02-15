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
            _logger.LogError("SMTP settings are not configured. Email cannot be sent.");
            throw new InvalidOperationException("Email configuration is missing. Please configure SMTP settings (MAIL_HOST, MAIL_USERNAME, MAIL_PASSWORD) in the .env file.");
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
        try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "EmailService.cs:74", message = "About to connect to SMTP", data = new { smtpHost = _emailSettings.SmtpHost, smtpPort = _emailSettings.SmtpPort, fromEmail = _emailSettings.FromEmail, fromName = _emailSettings.FromName }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
        // #endregion agent log

        using (var client = new SmtpClient())
        {
            try
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "EmailService.cs:80", message = "Connecting to SMTP server...", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                await client.ConnectAsync(_emailSettings.SmtpHost, _emailSettings.SmtpPort, SecureSocketOptions.StartTls);
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "EmailService.cs:84", message = "Connected! Now authenticating...", data = new { usernameLength = _emailSettings.SmtpUsername?.Length ?? 0, passwordLength = _emailSettings.SmtpPassword?.Length ?? 0 }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                await client.AuthenticateAsync(_emailSettings.SmtpUsername, _emailSettings.SmtpPassword);
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "EmailService.cs:88", message = "Authenticated! Sending email...", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                await client.SendAsync(message);
                
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "EmailService.cs:92", message = "Email sent! Disconnecting...", data = new { }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                await client.DisconnectAsync(true);
            }
            catch (Exception smtpEx)
            {
                // #region agent log
                try { System.IO.File.AppendAllText(@"d:\DauGia\.cursor\debug.log", System.Text.Json.JsonSerializer.Serialize(new { sessionId = "debug-session", runId = "run1", hypothesisId = "D", location = "EmailService.cs:98", message = "SMTP operation failed", data = new { exceptionType = smtpEx.GetType().Name, error = smtpEx.Message, innerError = smtpEx.InnerException?.Message }, timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }) + "\n"); } catch { }
                // #endregion agent log
                throw;
            }
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
            _logger.LogError("SendGrid API key is not configured. Email cannot be sent.");
            throw new InvalidOperationException("Email configuration is missing. Please configure SENDGRID_API_KEY in the .env file.");
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
            throw new InvalidOperationException($"Failed to send email via SendGrid. Status: {response.StatusCode}");
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

    public async Task SendPasswordResetOtpEmailAsync(string toEmail, string toName, string otpCode)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "PasswordResetOtpEmail.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{OtpCode}}", otpCode);
        
        await SendEmailAsync(toEmail, toName, "Mã xác nhận đặt lại mật khẩu - Realtime Auction Platform", htmlContent);
    }

    public async Task SendWelcomeEmailAsync(string toEmail, string toName)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "WelcomeEmail.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        
        await SendEmailAsync(toEmail, toName, "Welcome to Realtime Auction Platform", htmlContent);
    }

    // ===== AUCTION EMAIL NOTIFICATIONS =====

    public async Task SendAuctionEndingSoonEmailAsync(string toEmail, string toName, 
        string auctionTitle, string timeRemaining, string currentPrice, string auctionUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AuctionEndingSoon.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{TimeRemaining}}", timeRemaining);
        htmlContent = htmlContent.Replace("{{CurrentPrice}}", currentPrice);
        htmlContent = htmlContent.Replace("{{AuctionUrl}}", auctionUrl);
        
        await SendEmailAsync(toEmail, toName, $"⏰ Đấu giá sắp kết thúc: {auctionTitle}", htmlContent);
    }

    public async Task SendOutbidNotificationEmailAsync(string toEmail, string toName, 
        string auctionTitle, string yourBid, string newBid, string suggestedBid, string auctionUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "OutbidNotification.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{YourBid}}", yourBid);
        htmlContent = htmlContent.Replace("{{NewBid}}", newBid);
        htmlContent = htmlContent.Replace("{{SuggestedBid}}", suggestedBid);
        htmlContent = htmlContent.Replace("{{AuctionUrl}}", auctionUrl);
        
        await SendEmailAsync(toEmail, toName, $"⚡ Bạn đã bị vượt giá: {auctionTitle}", htmlContent);
    }

    public async Task SendAuctionWonEmailAsync(string toEmail, string toName, 
        string auctionTitle, string winningBid, string transactionUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "AuctionWon.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{WinningBid}}", winningBid);
        htmlContent = htmlContent.Replace("{{TransactionUrl}}", transactionUrl);
        
        await SendEmailAsync(toEmail, toName, $"🏆 Chúc mừng bạn thắng đấu giá: {auctionTitle}", htmlContent);
    }

    public async Task SendBuyoutBuyerEmailAsync(string toEmail, string toName, 
        string auctionTitle, string buyoutPrice, string transactionUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "BuyoutBuyer.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{BuyoutPrice}}", buyoutPrice);
        htmlContent = htmlContent.Replace("{{TransactionUrl}}", transactionUrl);
        
        await SendEmailAsync(toEmail, toName, $"⚡ Mua ngay thành công: {auctionTitle}", htmlContent);
    }

    public async Task SendBuyoutSellerEmailAsync(string toEmail, string toName, 
        string auctionTitle, string buyoutPrice, string buyerName, string transactionUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "BuyoutSeller.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{BuyoutPrice}}", buyoutPrice);
        htmlContent = htmlContent.Replace("{{BuyerName}}", buyerName);
        htmlContent = htmlContent.Replace("{{TransactionUrl}}", transactionUrl);
        
        await SendEmailAsync(toEmail, toName, $"💰 Sản phẩm đã được mua ngay: {auctionTitle}", htmlContent);
    }

    public async Task SendBidAcceptedEmailAsync(string toEmail, string toName, 
        string auctionTitle, string acceptedPrice, string sellerName, string transactionUrl)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "BidAccepted.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{AcceptedPrice}}", acceptedPrice);
        htmlContent = htmlContent.Replace("{{SellerName}}", sellerName);
        htmlContent = htmlContent.Replace("{{TransactionUrl}}", transactionUrl);
        
        await SendEmailAsync(toEmail, toName, $"🤝 Giá của bạn đã được chấp nhận: {auctionTitle}", htmlContent);
    }

    public async Task SendTransactionCompletedEmailAsync(string toEmail, string toName, 
        string role, string auctionTitle, string finalAmount, string transactionDate)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TransactionCompleted.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{Role}}", role);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{FinalAmount}}", finalAmount);
        htmlContent = htmlContent.Replace("{{TransactionDate}}", transactionDate);
        
        await SendEmailAsync(toEmail, toName, $"🎉 Giao dịch hoàn tất: {auctionTitle}", htmlContent);
    }

    public async Task SendTransactionReminderEmailAsync(string toEmail, string toName, 
        string auctionTitle, string status, int daysRemaining, string transactionUrl, string warningMessage)
    {
        var templatePath = Path.Combine(Directory.GetCurrentDirectory(), "Templates", "TransactionReminder.html");
        var htmlContent = await File.ReadAllTextAsync(templatePath);
        
        htmlContent = htmlContent.Replace("{{UserName}}", toName);
        htmlContent = htmlContent.Replace("{{AuctionTitle}}", auctionTitle);
        htmlContent = htmlContent.Replace("{{Status}}", status);
        htmlContent = htmlContent.Replace("{{DaysRemaining}}", daysRemaining.ToString());
        htmlContent = htmlContent.Replace("{{TransactionUrl}}", transactionUrl);
        htmlContent = htmlContent.Replace("{{WarningMessage}}", warningMessage);
        
        await SendEmailAsync(toEmail, toName, $"⏰ Nhắc nhở xác nhận giao dịch: {auctionTitle}", htmlContent);
    }

    // ===== ORDER EMAIL NOTIFICATIONS =====

    public async Task SendOrderShippedEmailAsync(string toEmail, string toName, 
        string productTitle, string? trackingNumber, string? shippingCarrier)
    {
        var trackingInfo = "";
        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            trackingInfo = $"<p><strong>Mã vận đơn:</strong> {trackingNumber}</p>";
            if (!string.IsNullOrWhiteSpace(shippingCarrier))
            {
                trackingInfo += $"<p><strong>Đơn vị vận chuyển:</strong> {shippingCarrier}</p>";
            }
        }

        var htmlContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
                .product {{ background: white; padding: 15px; border-radius: 8px; margin: 15px 0; }}
                .emoji {{ font-size: 48px; margin-bottom: 10px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <div class='emoji'>📦</div>
                    <h1>Đơn hàng đã được gửi đi!</h1>
                </div>
                <div class='content'>
                    <p>Xin chào <strong>{toName}</strong>,</p>
                    <p>Người bán đã gửi đơn hàng của bạn. Vui lòng theo dõi tình trạng giao hàng.</p>
                    
                    <div class='product'>
                        <p><strong>Sản phẩm:</strong> {productTitle}</p>
                        {trackingInfo}
                    </div>
                    
                    <p>Sau khi nhận được hàng, vui lòng xác nhận trong mục <strong>Đơn hàng của tôi</strong> để hoàn tất giao dịch.</p>
                    
                    <p>Trân trọng,<br>Realtime Auction Platform</p>
                </div>
            </div>
        </body>
        </html>";

        await SendEmailAsync(toEmail, toName, $"📦 Đơn hàng đã được gửi: {productTitle}", htmlContent);
    }

    // ===== WITHDRAWAL EMAIL NOTIFICATIONS =====

    public async Task SendWithdrawalOtpEmailAsync(string toEmail, string toName, string otpCode)
    {
        var htmlContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background: linear-gradient(135deg, #f093fb 0%, #f5576c 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
                .otp-code {{ font-size: 32px; font-weight: bold; color: #f5576c; text-align: center; padding: 20px; background: white; border-radius: 8px; letter-spacing: 8px; margin: 20px 0; }}
                .warning {{ color: #e74c3c; font-size: 14px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>Xac nhan rut tien</h1>
                </div>
                <div class='content'>
                    <p>Xin chao <strong>{toName}</strong>,</p>
                    <p>Ban da yeu cau rut tien tu vi. Vui long su dung ma OTP duoi day de xac nhan:</p>
                    <div class='otp-code'>{otpCode}</div>
                    <p class='warning'>Ma OTP co hieu luc trong 10 phut. Khong chia se ma nay voi bat ky ai.</p>
                    <p>Neu ban khong yeu cau rut tien, vui long bo qua email nay.</p>
                    <p>Tran trong,<br>Realtime Auction Platform</p>
                </div>
            </div>
        </body>
        </html>";

        await SendEmailAsync(toEmail, toName, "Ma xac nhan rut tien - Realtime Auction Platform", htmlContent);
    }

    public async Task SendWithdrawalApprovedEmailAsync(string toEmail, string toName, decimal amount, string bankInfo)
    {
        var htmlContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
                .info-box {{ background: white; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #667eea; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>Yeu cau rut tien da duoc duyet</h1>
                </div>
                <div class='content'>
                    <p>Xin chao <strong>{toName}</strong>,</p>
                    <p>Yeu cau rut tien cua ban da duoc admin duyet va dang xu ly chuyen khoan.</p>
                    <div class='info-box'>
                        <p><strong>So tien:</strong> {amount:N0} VND</p>
                        <p><strong>Tai khoan:</strong> {bankInfo}</p>
                    </div>
                    <p>Chung toi se thong bao khi chuyen khoan hoan tat.</p>
                    <p>Tran trong,<br>Realtime Auction Platform</p>
                </div>
            </div>
        </body>
        </html>";

        await SendEmailAsync(toEmail, toName, "Yeu cau rut tien da duoc duyet", htmlContent);
    }

    public async Task SendWithdrawalRejectedEmailAsync(string toEmail, string toName, decimal amount, string reason)
    {
        var htmlContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background: linear-gradient(135deg, #e74c3c 0%, #c0392b 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
                .reason-box {{ background: #fff5f5; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #e74c3c; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>Yeu cau rut tien bi tu choi</h1>
                </div>
                <div class='content'>
                    <p>Xin chao <strong>{toName}</strong>,</p>
                    <p>Yeu cau rut tien <strong>{amount:N0} VND</strong> da bi tu choi.</p>
                    <div class='reason-box'>
                        <p><strong>Ly do:</strong> {reason}</p>
                    </div>
                    <p>So tien da duoc hoan ve vi kha dung. Ban co the tao yeu cau rut tien moi.</p>
                    <p>Tran trong,<br>Realtime Auction Platform</p>
                </div>
            </div>
        </body>
        </html>";

        await SendEmailAsync(toEmail, toName, "Yeu cau rut tien bi tu choi", htmlContent);
    }

    public async Task SendWithdrawalCompletedEmailAsync(string toEmail, string toName,
        decimal amount, decimal fee, decimal finalAmount, string transactionCode, string bankInfo)
    {
        var feeInfo = fee > 0 ? $"<p><strong>Phi xu ly:</strong> {fee:N0} VND</p>" : "";
        var htmlContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background: linear-gradient(135deg, #11998e 0%, #38ef7d 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
                .info-box {{ background: white; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid #11998e; }}
                .success {{ color: #11998e; font-weight: bold; font-size: 18px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>Rut tien thanh cong!</h1>
                </div>
                <div class='content'>
                    <p>Xin chao <strong>{toName}</strong>,</p>
                    <p class='success'>Rut tien thanh cong!</p>
                    <div class='info-box'>
                        <p><strong>So tien yeu cau:</strong> {amount:N0} VND</p>
                        {feeInfo}
                        <p><strong>So tien nhan:</strong> {finalAmount:N0} VND</p>
                        <p><strong>Ma giao dich:</strong> {transactionCode}</p>
                        <p><strong>Tai khoan:</strong> {bankInfo}</p>
                    </div>
                    <p>Vui long kiem tra tai khoan ngan hang cua ban.</p>
                    <p>Tran trong,<br>Realtime Auction Platform</p>
                </div>
            </div>
        </body>
        </html>";

        await SendEmailAsync(toEmail, toName, "Rut tien thanh cong!", htmlContent);
    }

    public async Task SendWithdrawalReminderToAdminAsync(string toEmail, string toName,
        string withdrawalId, string userName, string userEmail, decimal amount, decimal finalAmount,
        string bankName, string accountLast4, int hoursSinceApproved, string message)
    {
        var urgencyClass = hoursSinceApproved >= 48 ? "urgent" : "warning";
        var urgencyColor = hoursSinceApproved >= 48 ? "#e74c3c" : "#f39c12";
        var urgencyIcon = hoursSinceApproved >= 48 ? "🚨" : "⚠️";

        var htmlContent = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <style>
                body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                .header {{ background: linear-gradient(135deg, {urgencyColor} 0%, #c0392b 100%); color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
                .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
                .info-box {{ background: white; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid {urgencyColor}; }}
                .message-box {{ background: #fff5f5; padding: 15px; border-radius: 8px; margin: 15px 0; border-left: 4px solid {urgencyColor}; font-weight: bold; }}
                .detail-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #eee; }}
                .detail-label {{ font-weight: bold; color: #555; }}
                .detail-value {{ color: #333; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='header'>
                    <h1>{urgencyIcon} Nhac nho: Rut tien can xu ly</h1>
                </div>
                <div class='content'>
                    <p>Xin chao <strong>{toName}</strong>,</p>
                    <div class='message-box'>
                        {message}
                    </div>
                    <div class='info-box'>
                        <div class='detail-row'>
                            <span class='detail-label'>Ma yeu cau:</span>
                            <span class='detail-value'>{withdrawalId}</span>
                        </div>
                        <div class='detail-row'>
                            <span class='detail-label'>Nguoi dung:</span>
                            <span class='detail-value'>{userName} ({userEmail})</span>
                        </div>
                        <div class='detail-row'>
                            <span class='detail-label'>So tien yeu cau:</span>
                            <span class='detail-value'>{amount:N0} VND</span>
                        </div>
                        <div class='detail-row'>
                            <span class='detail-label'>So tien nhan:</span>
                            <span class='detail-value'>{finalAmount:N0} VND</span>
                        </div>
                        <div class='detail-row'>
                            <span class='detail-label'>Tai khoan:</span>
                            <span class='detail-value'>{bankName} - {accountLast4}</span>
                        </div>
                        <div class='detail-row'>
                            <span class='detail-label'>Thoi gian da duyet:</span>
                            <span class='detail-value'>{hoursSinceApproved} gio truoc</span>
                        </div>
                    </div>
                    <p>Vui long vao Admin Dashboard de xu ly yeu cau rut tien nay.</p>
                    <p>Tran trong,<br>Realtime Auction Platform</p>
                </div>
            </div>
        </body>
        </html>";

        var subject = hoursSinceApproved >= 48 
            ? $"🚨 CẢNH BÁO: Yêu cầu rút tiền cần xử lý ngay - {hoursSinceApproved} giờ"
            : $"⚠️ Nhắc nhở: Yêu cầu rút tiền cần xử lý - {hoursSinceApproved} giờ";

        await SendEmailAsync(toEmail, toName, subject, htmlContent);
    }
}


