namespace RealtimeAuction.Api.Dtos.Admin;

public class DashboardChartsDto
{
    public List<RevenueDataPoint> RevenueChart { get; set; } = new();
    public List<UserGrowthDataPoint> UserGrowthChart { get; set; } = new();
    public List<CategoryDataPoint> CategoryDistribution { get; set; } = new();
    public AuctionCompletionRateDto AuctionCompletion { get; set; } = new();
}

public class RevenueDataPoint
{
    public string Date { get; set; } = null!;
    public decimal Revenue { get; set; }
    public int AuctionsCompleted { get; set; }
}

public class UserGrowthDataPoint
{
    public string Date { get; set; } = null!;
    public int TotalUsers { get; set; }
    public int NewUsers { get; set; }
}

public class CategoryDataPoint
{
    public string CategoryName { get; set; } = null!;
    public int AuctionCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class AuctionCompletionRateDto
{
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int Active { get; set; }
    public decimal CompletionRate { get; set; }
}
