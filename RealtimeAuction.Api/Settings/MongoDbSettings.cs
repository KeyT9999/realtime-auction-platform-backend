// Mục đích tệp: Trien khai logic/chuc nang chinh cua file MongoDbSettings.
namespace RealtimeAuction.Api.Settings;

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
}
