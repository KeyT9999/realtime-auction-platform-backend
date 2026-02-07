namespace RealtimeAuction.Api.Dtos.Admin;

public class UserAuctionDto
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Status { get; set; } = null!;
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public int BidCount { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string? WinnerId { get; set; }
    public string? EndReason { get; set; }
}
