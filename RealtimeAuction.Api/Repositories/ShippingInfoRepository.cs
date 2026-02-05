using MongoDB.Driver;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public class ShippingInfoRepository : IShippingInfoRepository
{
    private readonly IMongoCollection<ShippingInfo> _collection;

    public ShippingInfoRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ShippingInfo>("ShippingInfo");
    }

    public async Task<ShippingInfo?> GetByIdAsync(string id)
    {
        return await _collection.Find(s => s.Id == id).FirstOrDefaultAsync();
    }

    public async Task<ShippingInfo?> GetByAuctionIdAsync(string auctionId)
    {
        return await _collection.Find(s => s.AuctionId == auctionId).FirstOrDefaultAsync();
    }

    public async Task<ShippingInfo> CreateAsync(ShippingInfo shippingInfo)
    {
        shippingInfo.CreatedAt = DateTime.UtcNow;
        shippingInfo.UpdatedAt = DateTime.UtcNow;
        await _collection.InsertOneAsync(shippingInfo);
        return shippingInfo;
    }

    public async Task<ShippingInfo> UpdateAsync(ShippingInfo shippingInfo)
    {
        shippingInfo.UpdatedAt = DateTime.UtcNow;
        await _collection.ReplaceOneAsync(s => s.Id == shippingInfo.Id, shippingInfo);
        return shippingInfo;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _collection.DeleteOneAsync(s => s.Id == id);
        return result.DeletedCount > 0;
    }
}
