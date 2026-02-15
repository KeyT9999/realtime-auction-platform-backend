using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Withdrawal;

public class AdminRejectWithdrawalRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do từ chối")]
    public string Reason { get; set; } = null!;
}
