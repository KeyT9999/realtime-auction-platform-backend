namespace RealtimeAuction.Api.Dtos.Watchlist;

public class WatchlistResponseDto
{
    public string Id { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string? UserName { get; set; }
    public string AuctionId { get; set; } = null!;
    public AuctionSummaryDto? Auction { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuctionSummaryDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public decimal CurrentPrice { get; set; }
    public DateTime EndTime { get; set; }
    public string? ImageUrl { get; set; }
}
