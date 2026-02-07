namespace RealtimeAuction.Api.Dtos.Admin;

public class UserBidDto
{
    public string Id { get; set; } = null!;
    public string AuctionId { get; set; } = null!;
    public string AuctionTitle { get; set; } = null!;
    public decimal BidAmount { get; set; }
    public DateTime BidTime { get; set; }
    public bool IsWinning { get; set; }
    public string Status { get; set; } = null!; // "Active", "Won", "Lost", "Refunded"
}
