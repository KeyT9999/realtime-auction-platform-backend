using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Withdrawal;

public class CreateWithdrawalRequest
{
    [Required]
    [Range(50000, double.MaxValue, ErrorMessage = "Số tiền rút tối thiểu là 50,000 VND")]
    public decimal Amount { get; set; }

    [Required]
    public string BankAccountId { get; set; } = null!;
}
