namespace RealtimeAuction.Api.Dtos.Admin;

public class SubtractBalanceRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
