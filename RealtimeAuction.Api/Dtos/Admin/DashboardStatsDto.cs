namespace RealtimeAuction.Api.Dtos.Admin;

public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int UsersOnline { get; set; } // Users active in last 15 minutes
    public int NewUsersToday { get; set; }
    public int ActiveAuctions { get; set; }
    public int CompletedAuctionsToday { get; set; }
    public decimal TodayRevenue { get; set; }
    public int BidsInLastHour { get; set; }
    public GrowthTrends Trends { get; set; } = new();
}

public class GrowthTrends
{
    public decimal UserGrowthDay { get; set; } // % change from yesterday
    public decimal UserGrowthWeek { get; set; }
    public decimal AuctionGrowthDay { get; set; }
    public decimal RevenueGrowthDay { get; set; }
}
