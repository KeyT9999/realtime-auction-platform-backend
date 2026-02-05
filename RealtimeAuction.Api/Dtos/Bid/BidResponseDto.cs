namespace RealtimeAuction.Api.Dtos.Bid;

public class BidResponseDto
{
    public string Id { get; set; } = null!;
    public string AuctionId { get; set; } = null!;
    public string? AuctionTitle { get; set; }
    public string UserId { get; set; } = null!;
    public string? UserName { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsWinningBid { get; set; }
    public AutoBidSettingsDto? AutoBid { get; set; }
    public DateTime CreatedAt { get; set; }
}

