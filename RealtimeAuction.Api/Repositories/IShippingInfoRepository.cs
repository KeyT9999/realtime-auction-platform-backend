using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IShippingInfoRepository
{
    Task<ShippingInfo?> GetByIdAsync(string id);
    Task<ShippingInfo?> GetByAuctionIdAsync(string auctionId);
    Task<ShippingInfo> CreateAsync(ShippingInfo shippingInfo);
    Task<ShippingInfo> UpdateAsync(ShippingInfo shippingInfo);
    Task<bool> DeleteAsync(string id);
}
