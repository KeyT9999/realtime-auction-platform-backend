using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Withdrawal;

public class AdminCompleteWithdrawalRequest
{
    [Required(ErrorMessage = "Vui lòng nhập mã giao dịch ngân hàng")]
    public string TransactionCode { get; set; } = null!;

    /// <summary>
    /// Số tiền thực tế đã chuyển. Nếu không có, dùng FinalAmount từ withdrawal.
    /// Phải đúng với FinalAmount (không cho sai).
    /// </summary>
    public decimal? ActualAmount { get; set; }

    /// <summary>
    /// URL của proof (screenshot/ảnh chụp giao dịch). Có thể upload trước và gửi URL.
    /// </summary>
    public string? TransactionProof { get; set; }
}
