namespace RealtimeAuction.Api.Dtos.User;

public class SellerStatsResponse
{
    public int TotalAuctions { get; set; }
    public int CompletedAuctions { get; set; }
    public int ActiveAuctions { get; set; }
    public decimal CompletionRate { get; set; }
    public DateTime JoinedDate { get; set; }
    public decimal? AverageRating { get; set; } // For future rating system
}
