namespace RealtimeAuction.Api.Models.Enums;

public enum OrderStatus
{
    PendingShipment = 0,  // Chờ người bán gửi hàng
    Shipped = 1,          // Đang vận chuyển
    Completed = 2,        // Đã hoàn tất
    Cancelled = 3         // Đã hủy / Hoàn tiền
}
