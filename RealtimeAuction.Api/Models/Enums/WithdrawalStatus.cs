// Mục đích tệp: Dinh nghia cau truc du lieu hoac thuc the cho WithdrawalStatus.
namespace RealtimeAuction.Api.Models.Enums;

public enum WithdrawalStatus
{
    Pending = 0,        // Chờ OTP
    OtpVerified = 1,    // Đã xác nhận OTP, chờ admin duyệt
    Processing = 2,     // Admin đã duyệt, đang chuyển tiền
    Completed = 3,      // Hoàn tất
    Rejected = 4,       // Admin từ chối
    Cancelled = 5       // User hủy
}
