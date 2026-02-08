using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IMongoCollection<Order> _orders;

    public OrderRepository(IMongoDatabase database)
    {
        _orders = database.GetCollection<Order>("Orders");
    }

    public async Task<Order?> GetByIdAsync(string id)
    {
        return await _orders.Find(o => o.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Order?> GetByAuctionIdAsync(string auctionId)
    {
        return await _orders.Find(o => o.AuctionId == auctionId).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Order>> GetByBuyerIdAsync(string buyerId)
    {
        return await _orders
            .Find(o => o.BuyerId == buyerId)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Order>> GetBySellerIdAsync(string sellerId)
    {
        return await _orders
            .Find(o => o.SellerId == sellerId)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
    }

    public async Task<Order> CreateAsync(Order order)
    {
        await _orders.InsertOneAsync(order);
        return order;
    }

    public async Task UpdateAsync(Order order)
    {
        order.UpdatedAt = DateTime.UtcNow;
        await _orders.ReplaceOneAsync(o => o.Id == order.Id, order);
    }

    public async Task<IEnumerable<Order>> GetByStatusAsync(OrderStatus status)
    {
        return await _orders
            .Find(o => o.Status == status)
            .SortByDescending(o => o.CreatedAt)
            .ToListAsync();
    }
}
