using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IBidRepository
{
    Task<Bid?> GetByIdAsync(string id);
    Task<Bid> CreateAsync(Bid bid, decimal currentPrice, decimal bidIncrement);
    Task<Bid> UpdateAsync(Bid bid);
    Task<List<Bid>> GetByAuctionIdAsync(string auctionId);
    Task<List<Bid>> GetByUserIdAsync(string userId);
    Task<Bid?> GetHighestBidAsync(string auctionId);
    Task<Bid?> GetWinningBidAsync(string auctionId);
    Task SetWinningBidAsync(string auctionId, string winningBidId);
    Task<List<Bid>> GetAllAsync();
}
