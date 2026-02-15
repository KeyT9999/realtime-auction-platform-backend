using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Withdrawal;

public class VerifyWithdrawalOtpRequest
{
    [Required]
    public string WithdrawalId { get; set; } = null!;

    [Required]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số")]
    public string OtpCode { get; set; } = null!;
}
