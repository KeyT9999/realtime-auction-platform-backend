using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly IMongoCollection<Auction> _auctions;

    public AuctionRepository(IMongoDatabase database)
    {
        _auctions = database.GetCollection<Auction>("Auctions");
    }

    public async Task<Auction?> GetByIdAsync(string id)
    {
        return await _auctions.Find(a => a.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Auction> CreateAsync(Auction auction)
    {
        auction.CreatedAt = DateTime.UtcNow;
        auction.UpdatedAt = DateTime.UtcNow;
        await _auctions.InsertOneAsync(auction);
        return auction;
    }

    public async Task<Auction> UpdateAsync(Auction auction)
    {
        auction.UpdatedAt = DateTime.UtcNow;
        await _auctions.ReplaceOneAsync(a => a.Id == auction.Id, auction);
        return auction;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _auctions.DeleteOneAsync(a => a.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<Auction>> GetAllAsync()
    {
        return await _auctions.Find(_ => true).ToListAsync();
    }

    public async Task<List<Auction>> GetByStatusAsync(AuctionStatus status)
    {
        return await _auctions.Find(a => a.Status == status).ToListAsync();
    }

    public async Task<List<Auction>> GetBySellerIdAsync(string sellerId)
    {
        return await _auctions.Find(a => a.SellerId == sellerId).ToListAsync();
    }

    public async Task<List<Auction>> GetByCategoryIdAsync(string categoryId)
    {
        return await _auctions.Find(a => a.CategoryId == categoryId).ToListAsync();
    }

    public async Task<List<Auction>> GetActiveAuctionsAsync()
    {
        var now = DateTime.UtcNow;
        return await _auctions.Find(a => 
            a.Status == AuctionStatus.Active && 
            a.StartTime <= now && 
            a.EndTime >= now).ToListAsync();
    }

    public async Task<Auction> UpdateCurrentPriceAsync(string auctionId, decimal newPrice)
    {
        var update = Builders<Auction>.Update
            .Set(a => a.CurrentPrice, newPrice)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _auctions.UpdateOneAsync(a => a.Id == auctionId, update);
        
        var updatedAuction = await GetByIdAsync(auctionId);
        if (updatedAuction == null)
        {
            throw new Exception($"Auction with id {auctionId} not found after update");
        }
        return updatedAuction;
    }

    public async Task<bool> ValidateStatusTransitionAsync(AuctionStatus currentStatus, AuctionStatus newStatus)
    {
        // If status is the same, it's valid
        if (currentStatus == newStatus)
        {
            return true;
        }

        // Define valid transitions
        return currentStatus switch
        {
            AuctionStatus.Draft => newStatus == AuctionStatus.Active || newStatus == AuctionStatus.Cancelled,
            AuctionStatus.Active => newStatus == AuctionStatus.Pending || newStatus == AuctionStatus.Cancelled,
            AuctionStatus.Pending => newStatus == AuctionStatus.Completed || newStatus == AuctionStatus.Cancelled,
            AuctionStatus.Completed => false, // Cannot transition from Completed
            AuctionStatus.Cancelled => false, // Cannot transition from Cancelled
            _ => false
        };
    }
}
