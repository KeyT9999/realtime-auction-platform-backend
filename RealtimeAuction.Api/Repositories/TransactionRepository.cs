using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly IMongoCollection<Transaction> _transactions;

    public TransactionRepository(MongoDbSettings mongoDbSettings)
    {
        var client = new MongoClient(mongoDbSettings.ConnectionString);
        var database = client.GetDatabase(mongoDbSettings.DatabaseName);
        _transactions = database.GetCollection<Transaction>("Transactions");
    }

    public async Task<Transaction> CreateAsync(Transaction transaction)
    {
        await _transactions.InsertOneAsync(transaction);
        return transaction;
    }

    public async Task<Transaction> UpdateAsync(Transaction transaction)
    {
        transaction.UpdatedAt = DateTime.UtcNow;
        await _transactions.ReplaceOneAsync(t => t.Id == transaction.Id, transaction);
        return transaction;
    }

    public async Task<List<Transaction>> GetByUserIdAsync(string userId)
    {
        return await _transactions
            .Find(t => t.UserId == userId)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetByUserIdAndTypeAsync(string userId, TransactionType type)
    {
        return await _transactions
            .Find(t => t.UserId == userId && t.Type == type)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Transaction?> GetByIdAsync(string id)
    {
        return await _transactions.Find(t => t.Id == id).FirstOrDefaultAsync();
    }

    public async Task<List<Transaction>> GetByPayOsOrderCodeAsync(long orderCode)
    {
        return await _transactions
            .Find(t => t.PayOsOrderCode == orderCode)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Transaction>> GetByAuctionIdAsync(string auctionId)
    {
        return await _transactions
            .Find(t => t.RelatedAuctionId == auctionId)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();
    }
}
