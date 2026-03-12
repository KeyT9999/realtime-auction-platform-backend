namespace RealtimeAuction.Api.Services;

public interface IEscrowService
{
    /// <summary>
    /// Đóng băng tiền buyer vào Escrow ngay khi Order được tạo sau khi thắng đấu giá.
    /// Tiền đã có trong EscrowBalance (từ bid hold), chỉ cần ghi metadata vào Order.
    /// </summary>
    Task<bool> FreezeEscrowAsync(string orderId);

    /// <summary>
    /// Giải phóng Escrow → Seller.AvailableBalance (buyer xác nhận hoặc auto-release).
    /// </summary>
    Task<bool> ReleaseEscrowToSellerAsync(string orderId, string releaseReason);

    /// <summary>
    /// Hoàn tiền Escrow → Buyer.AvailableBalance (hủy đơn hoặc admin phân xử buyer thắng).
    /// </summary>
    Task<bool> RefundEscrowToBuyerAsync(string orderId, string refundReason);

    /// <summary>
    /// Chạy tự động để release các orders đã quá hạn EscrowAutoReleaseAt.
    /// Được gọi bởi BackgroundService định kỳ.
    /// </summary>
    Task ProcessAutoReleaseAsync();
}
