namespace RealtimeAuction.Api.Models.Enums;

public enum TransactionType
{
    Deposit = 0,           // Nạp tiền (từ PayOS)
    Withdraw = 1,          // Rút tiền
    Hold = 2,              // Giữ cọc khi đặt giá (Available → Held)
    Release = 3,           // Mở khóa cọc khi bị outbid (Held → Available)
    Payment = 4,           // Thanh toán khi thắng (Held → Seller)
    Refund = 5,            // Hoàn tiền khi hủy giao dịch
    AdminAdjustment = 6,   // Admin điều chỉnh số dư
    WithdrawalHold = 7,    // Giữ tiền khi tạo yêu cầu rút (Available → HeldBalance)
    WithdrawalRelease = 8, // Hoàn tiền khi hủy/từ chối rút (HeldBalance → Available)
    EscrowFreeze = 9,      // Đóng băng tiền escrow khi auction kết thúc (EscrowBalance frozen)
    EscrowRelease = 10,    // Giải phóng escrow sang người bán (Escrow → Seller.Available)
    EscrowRefund = 11      // Hoàn tiền từ escrow về người mua (Escrow → Buyer.Available)
}
