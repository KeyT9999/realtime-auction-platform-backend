using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IWatchlistRepository
{
    Task<Watchlist?> GetByIdAsync(string id);
    Task<Watchlist> AddAsync(Watchlist watchlist);
    Task<bool> RemoveAsync(string id);
    Task<bool> RemoveByUserAndAuctionAsync(string userId, string auctionId);
    Task<List<Watchlist>> GetByUserIdAsync(string userId);
    Task<List<Watchlist>> GetByAuctionIdAsync(string auctionId);
    Task<bool> IsWatchingAsync(string userId, string auctionId);
    Task<List<Watchlist>> GetAllAsync();
}
