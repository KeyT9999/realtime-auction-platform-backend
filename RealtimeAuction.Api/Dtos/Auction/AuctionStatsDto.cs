namespace RealtimeAuction.Api.Dtos.Auction;

public class AuctionStatsDto
{
    public int TotalAuctions { get; set; }
    public int ActiveAuctions { get; set; }
    public int DraftAuctions { get; set; }
    public int CompletedAuctions { get; set; }
    public int CancelledAuctions { get; set; }
    public int PendingAuctions { get; set; }
    public decimal? TotalRevenue { get; set; }
    public decimal? AveragePrice { get; set; }
}
