// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho AddBalanceRequest.
namespace RealtimeAuction.Api.Dtos.Admin;

public class AddBalanceRequest
{
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}
