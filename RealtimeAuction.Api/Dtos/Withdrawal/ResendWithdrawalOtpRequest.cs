// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho ResendWithdrawalOtpRequest.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Withdrawal;

public class ResendWithdrawalOtpRequest
{
    [Required]
    public string WithdrawalId { get; set; } = null!;
}
