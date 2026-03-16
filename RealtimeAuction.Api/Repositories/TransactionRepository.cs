using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly IMongoCollection<Transaction> _transactions;

    public TransactionRepository(IMongoDatabase database)
    {
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

    public async Task<List<Transaction>> GetAllAsync()
    {
        return await _transactions
            .Find(_ => true)
            .SortByDescending(t => t.CreatedAt)
            .ToListAsync();
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

    public async Task<(List<Transaction> Items, int TotalCount)> GetPagedByUserIdAsync(
        string userId,
        int page,
        int limit,
        TransactionType? type = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null)
    {
        var filterBuilder = Builders<Transaction>.Filter;
        var filters = new List<FilterDefinition<Transaction>>
        {
            filterBuilder.Eq(t => t.UserId, userId)
        };

        if (type.HasValue)
        {
            filters.Add(filterBuilder.Eq(t => t.Type, type.Value));
        }

        if (dateFrom.HasValue)
        {
            filters.Add(filterBuilder.Gte(t => t.CreatedAt, dateFrom.Value));
        }

        if (dateTo.HasValue)
        {
            filters.Add(filterBuilder.Lt(t => t.CreatedAt, dateTo.Value));
        }

        var filter = filters.Count == 1 ? filters[0] : filterBuilder.And(filters);
        var totalCount = (int)await _transactions.CountDocumentsAsync(filter);
        var items = await _transactions
            .Find(filter)
            .SortByDescending(t => t.CreatedAt)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync();

        return (items, totalCount);
    }
}
