using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Dtos.Product;

namespace RealtimeAuction.Api.Dtos.Auction;

public class AuctionResponseDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal? ReservePrice { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int Duration { get; set; }
    public AuctionStatus Status { get; set; }
    public string SellerId { get; set; } = null!;
    public string? SellerName { get; set; }
    public string CategoryId { get; set; } = null!;
    public string? CategoryName { get; set; }
    public string ProductId { get; set; } = null!;
    public ProductResponseDto? Product { get; set; }
    public List<string> Images { get; set; } = new();
    public decimal BidIncrement { get; set; }
    public int? AutoExtendDuration { get; set; }
    public decimal? BuyoutPrice { get; set; }
    public string? WinnerId { get; set; }
    public string? WinnerName { get; set; }
    public string? EndReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int BidCount { get; set; }
}
