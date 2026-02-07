namespace RealtimeAuction.Api.Dtos.Admin;

public class AddBalanceRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
