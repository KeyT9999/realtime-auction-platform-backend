namespace RealtimeAuction.Api.Dtos.Admin;

public class UserTransactionDto
{
    public string Id { get; set; } = null!;
    public string Type { get; set; } = null!; // "Deposit", "Withdraw", "Escrow", "Refund", "Payment"
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RelatedAuctionId { get; set; }
}
