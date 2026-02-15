using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Withdrawal;

public class ResendWithdrawalOtpRequest
{
    [Required]
    public string WithdrawalId { get; set; } = null!;
}
