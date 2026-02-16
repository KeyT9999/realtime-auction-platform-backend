using MongoDB.Driver;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public class BidRepository : IBidRepository
{
    private readonly IMongoCollection<Bid> _bids;

    public BidRepository(IMongoDatabase database)
    {
        _bids = database.GetCollection<Bid>("Bids");
    }

    public async Task<Bid?> GetByIdAsync(string id)
    {
        return await _bids.Find(b => b.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Bid> CreateAsync(Bid bid, decimal currentPrice, decimal bidIncrement)
    {
        // Validate BidIncrement: new bid must be >= CurrentPrice + BidIncrement
        var minimumBid = currentPrice + bidIncrement;
        if (bid.Amount < minimumBid)
        {
            throw new ArgumentException($"Bid amount {bid.Amount} must be at least {minimumBid} (CurrentPrice {currentPrice} + BidIncrement {bidIncrement})");
        }

        bid.Timestamp = DateTime.UtcNow;
        bid.CreatedAt = DateTime.UtcNow;
        bid.IsWinningBid = false; // Will be set to true by service layer if it becomes the highest bid

        await _bids.InsertOneAsync(bid);
        return bid;
    }

    public async Task<List<Bid>> GetByAuctionIdAsync(string auctionId)
    {
        return await _bids.Find(b => b.AuctionId == auctionId)
            .SortByDescending(b => b.Amount)
            .ThenByDescending(b => b.Timestamp)
            .ToListAsync();
    }

    public async Task<List<Bid>> GetByUserIdAsync(string userId)
    {
        return await _bids.Find(b => b.UserId == userId)
            .SortByDescending(b => b.Timestamp)
            .ToListAsync();
    }

    public async Task<Bid?> GetHighestBidAsync(string auctionId)
    {
        return await _bids.Find(b => b.AuctionId == auctionId)
            .SortByDescending(b => b.Amount)
            .ThenByDescending(b => b.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task<Bid?> GetWinningBidAsync(string auctionId)
    {
        return await _bids.Find(b => b.AuctionId == auctionId && b.IsWinningBid == true)
            .FirstOrDefaultAsync();
    }

    public async Task SetWinningBidAsync(string auctionId, string winningBidId)
    {
        // Clear previous winning flags (best-effort)
        var clearUpdate = Builders<Bid>.Update.Set(b => b.IsWinningBid, false);
        await _bids.UpdateManyAsync(b => b.AuctionId == auctionId, clearUpdate);

        // Set the new winning bid
        var setUpdate = Builders<Bid>.Update.Set(b => b.IsWinningBid, true);
        await _bids.UpdateOneAsync(b => b.Id == winningBidId && b.AuctionId == auctionId, setUpdate);
    }

    public async Task<List<Bid>> GetAllAsync()
    {
        return await _bids.Find(_ => true).ToListAsync();
    }

    public async Task<Bid> UpdateAsync(Bid bid)
    {
        await _bids.ReplaceOneAsync(b => b.Id == bid.Id, bid);
        return bid;
    }
}
