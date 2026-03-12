namespace RealtimeAuction.Api.Models.Enums;

public enum OrderStatus
{
    PendingShipment = 0,  // Chờ người bán gửi hàng (tiền đang frozen trong Escrow)
    Shipped = 1,          // Đang vận chuyển (Escrow vẫn frozen, buyer chưa xác nhận)
    Completed = 2,        // Đã hoàn tất (Escrow đã release sang seller)
    Cancelled = 3,        // Đã hủy / Hoàn tiền (Escrow đã refund về buyer)
    Disputed = 4          // Đang tranh chấp (Escrow bị freeze, chờ admin phân xử)
}
