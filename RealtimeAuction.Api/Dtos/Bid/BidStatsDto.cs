namespace RealtimeAuction.Api.Dtos.Bid;

public class BidStatsDto
{
    public int TotalBids { get; set; }
    public decimal? HighestBid { get; set; }
    public decimal? AverageBid { get; set; }
    public int BidsToday { get; set; }
    public int BidsThisWeek { get; set; }
    public int BidsThisMonth { get; set; }
}
