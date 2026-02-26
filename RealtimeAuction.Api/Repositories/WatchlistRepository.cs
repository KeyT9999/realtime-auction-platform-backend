using MongoDB.Driver;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public class WatchlistRepository : IWatchlistRepository
{
    private readonly IMongoCollection<Watchlist> _watchlists;

    public WatchlistRepository(IMongoDatabase database)
    {
        _watchlists = database.GetCollection<Watchlist>("Watchlists");
    }

    public async Task<Watchlist?> GetByIdAsync(string id)
    {
        return await _watchlists.Find(w => w.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Watchlist> AddAsync(Watchlist watchlist)
    {
        watchlist.CreatedAt = DateTime.UtcNow;
        await _watchlists.InsertOneAsync(watchlist);
        return watchlist;
    }

    public async Task<Watchlist> UpdateAsync(Watchlist watchlist)
    {
        if (string.IsNullOrEmpty(watchlist.Id))
            throw new ArgumentException("Watchlist Id is required for update.", nameof(watchlist));
        await _watchlists.ReplaceOneAsync(w => w.Id == watchlist.Id, watchlist);
        return watchlist;
    }

    public async Task<bool> RemoveAsync(string id)
    {
        var result = await _watchlists.DeleteOneAsync(w => w.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<bool> RemoveByUserAndAuctionAsync(string userId, string auctionId)
    {
        // Remove all matching entries (allows duplicates, so may remove multiple)
        var result = await _watchlists.DeleteManyAsync(w => w.UserId == userId && w.AuctionId == auctionId);
        return result.DeletedCount > 0;
    }

    public async Task<List<Watchlist>> GetByUserIdAsync(string userId)
    {
        return await _watchlists.Find(w => w.UserId == userId)
            .SortByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Watchlist>> GetByAuctionIdAsync(string auctionId)
    {
        return await _watchlists.Find(w => w.AuctionId == auctionId)
            .SortByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> IsWatchingAsync(string userId, string auctionId)
    {
        var count = await _watchlists.CountDocumentsAsync(w => w.UserId == userId && w.AuctionId == auctionId);
        return count > 0;
    }

    public async Task<List<Watchlist>> GetAllAsync()
    {
        return await _watchlists.Find(_ => true).ToListAsync();
    }
}
