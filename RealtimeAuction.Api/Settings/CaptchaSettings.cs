namespace RealtimeAuction.Api.Settings;

public class CaptchaSettings
{
    public bool Enabled { get; set; } = true;
    public string SecretKey { get; set; } = string.Empty;
    public double MinimumScore { get; set; } = 0.5d;
    public string VerifyUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";
    public int TimeoutSeconds { get; set; } = 10;
}
