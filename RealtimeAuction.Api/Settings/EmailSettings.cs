namespace RealtimeAuction.Api.Settings;

public class EmailSettings
{
    // SendGrid settings
    public string ApiKey { get; set; } = string.Empty;
    
    // SMTP settings
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public bool UseSmtp { get; set; } = false; // Nếu true thì dùng SMTP, false thì dùng SendGrid
    
    // Common settings
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string ReplyTo { get; set; } = string.Empty;
}
