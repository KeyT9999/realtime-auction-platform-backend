using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.BankAccount;

public class CreateBankAccountRequest
{
    [Required(ErrorMessage = "Vui lòng nhập tên ngân hàng")]
    public string BankName { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập số tài khoản")]
    public string AccountNumber { get; set; } = null!;

    [Required(ErrorMessage = "Vui lòng nhập tên chủ tài khoản")]
    public string AccountName { get; set; } = null!;

    public bool IsDefault { get; set; } = false;
}
