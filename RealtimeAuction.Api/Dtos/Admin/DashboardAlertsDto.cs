namespace RealtimeAuction.Api.Dtos.Admin;

public class DashboardAlertsDto
{
    public List<SystemAlert> Alerts { get; set; } = new();
    public int TotalAlerts { get; set; }
    public int CriticalAlerts { get; set; }
    public int WarningAlerts { get; set; }
}

public class SystemAlert
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = null!; // "critical", "warning", "info"
    public string Message { get; set; } = null!;
    public string? EntityType { get; set; } // "User", "Auction", "Bid"
    public string? EntityId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
