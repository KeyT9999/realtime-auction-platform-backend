namespace RealtimeAuction.Api.Models.Enums;

public enum TransactionType
{
    Deposit = 0,          // Nạp tiền (từ PayOS)
    Withdraw = 1,         // Rút tiền
    Hold = 2,             // Giữ cọc khi đặt giá (Available → Held)
    Release = 3,          // Mở khóa cọc khi bị outbid (Held → Available)
    Payment = 4,          // Thanh toán khi thắng (Held → Seller)
    Refund = 5,           // Hoàn tiền khi hủy giao dịch
    AdminAdjustment = 6,  // Admin điều chỉnh số dư
    WithdrawalHold = 7,   // Giữ tiền khi tạo yêu cầu rút (Available → Escrow)
    WithdrawalRelease = 8 // Hoàn tiền khi hủy/từ chối rút (Escrow → Available)
}
