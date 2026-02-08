using RealtimeAuction.Api.Dtos.Order;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services;

public interface IOrderService
{
    /// <summary>
    /// Create order from completed auction
    /// </summary>
    Task<Order> CreateOrderFromAuctionAsync(string auctionId, string buyerId, string sellerId, decimal amount, string productTitle, string? productImage);

    /// <summary>
    /// Get order by ID
    /// </summary>
    Task<Order?> GetByIdAsync(string orderId);

    /// <summary>
    /// Get all orders for buyer
    /// </summary>
    Task<IEnumerable<OrderDto>> GetBuyerOrdersAsync(string userId);

    /// <summary>
    /// Get all orders for seller
    /// </summary>
    Task<IEnumerable<OrderDto>> GetSellerOrdersAsync(string userId);

    /// <summary>
    /// Seller marks order as shipped
    /// </summary>
    Task<OrderResult> MarkAsShippedAsync(string orderId, string userId, ShipOrderRequest request);

    /// <summary>
    /// Buyer confirms receipt and releases payment
    /// </summary>
    Task<OrderResult> ConfirmReceivedAsync(string orderId, string userId);

    /// <summary>
    /// Cancel order and refund buyer
    /// </summary>
    Task<OrderResult> CancelOrderAsync(string orderId, string userId, string? reason);
}
