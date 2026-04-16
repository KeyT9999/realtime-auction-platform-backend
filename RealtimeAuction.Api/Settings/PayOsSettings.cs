// Mục đích tệp: Trien khai logic/chuc nang chinh cua file PayOsSettings.
namespace RealtimeAuction.Api.Settings;

public class PayOsSettings
{
    public string ClientId { get; set; } = null!;
    public string ApiKey { get; set; } = null!;
    public string ChecksumKey { get; set; } = null!;
    public string ReturnUrl { get; set; } = null!;
    public string CancelUrl { get; set; } = null!;
}
