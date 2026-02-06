namespace RealtimeAuction.Api.Dtos.Admin;

public class DashboardActivitiesDto
{
    public List<ActivityItem> RecentBids { get; set; } = new();
    public List<ActivityItem> NewAuctions { get; set; } = new();
    public List<ActivityItem> NewUsers { get; set; } = new();
    public List<ActivityItem> CompletedAuctions { get; set; } = new();
}

public class ActivityItem
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!; // "bid", "auction", "user"
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? UserName { get; set; }
    public decimal? Amount { get; set; }
}
