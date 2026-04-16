// Mục đích tệp: Trien khai logic/chuc nang chinh cua file GeminiSettings.
namespace RealtimeAuction.Api.Settings;

public class GeminiSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-2.5-flash";
    public int TimeoutSeconds { get; set; } = 60;
}
