// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho UpdateBankAccountRequest.
namespace RealtimeAuction.Api.Dtos.BankAccount;

public class UpdateBankAccountRequest
{
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public bool? IsDefault { get; set; }
}
