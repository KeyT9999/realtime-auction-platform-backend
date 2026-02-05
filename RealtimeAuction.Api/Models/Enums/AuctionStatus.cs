namespace RealtimeAuction.Api.Models.Enums;

public enum AuctionStatus
{
    Draft = 0,      // Nháp - chưa công khai
    Active = 1,     // Đang diễn ra
    Pending = 2,   // Chờ xử lý (sau khi kết thúc)
    Completed = 3, // Hoàn thành (đã có người thắng)
    Cancelled = 4  // Đã hủy
}
