namespace RealtimeAuction.Api.Models.Enums;

public enum DisputeStatus
{
    Open = 0,              // Mới tạo, chờ xử lý
    UnderReview = 1,       // Admin đang xem xét
    ResolvedBuyerWins = 2, // Buyer thắng → hoàn tiền
    ResolvedSellerWins = 3,// Seller thắng
    Closed = 4             // Đóng / Không hợp lệ
}
