using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public interface IAuctionRepository
{
    Task<Auction?> GetByIdAsync(string id);
    Task<Auction> CreateAsync(Auction auction);
    Task<Auction> UpdateAsync(Auction auction);
    Task<bool> DeleteAsync(string id);
    Task<List<Auction>> GetAllAsync();
    Task<List<Auction>> GetByStatusAsync(AuctionStatus status);
    Task<List<Auction>> GetBySellerIdAsync(string sellerId);
    Task<List<Auction>> GetByCategoryIdAsync(string categoryId);
    Task<List<Auction>> GetActiveAuctionsAsync();
    Task<Auction> UpdateCurrentPriceAsync(string auctionId, decimal newPrice);
    Task<bool> ValidateStatusTransitionAsync(AuctionStatus currentStatus, AuctionStatus newStatus);
}
