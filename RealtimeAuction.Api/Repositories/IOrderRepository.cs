using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string id);
    Task<Order?> GetByAuctionIdAsync(string auctionId);
    Task<IEnumerable<Order>> GetByBuyerIdAsync(string buyerId);
    Task<IEnumerable<Order>> GetBySellerIdAsync(string sellerId);
    Task<Order> CreateAsync(Order order);
    Task UpdateAsync(Order order);
    Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status);
    /// <summary>Lấy các Orders đã shipped, quá hạn auto-release và chưa được release</summary>
    Task<IEnumerable<Order>> GetOrdersForAutoReleaseAsync();
}

