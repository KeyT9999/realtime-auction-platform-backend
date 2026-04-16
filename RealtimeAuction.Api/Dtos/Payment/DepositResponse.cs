// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho DepositResponse.
namespace RealtimeAuction.Api.Dtos.Payment;

public class DepositResponse
{
    public long OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string CheckoutUrl { get; set; } = null!;
    public string QrCode { get; set; } = null!;
    public string Status { get; set; } = null!;
}
