using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public class DisputeRepository : IDisputeRepository
{
    private readonly IMongoCollection<Dispute> _disputes;

    public DisputeRepository(IMongoDatabase database)
    {
        _disputes = database.GetCollection<Dispute>("disputes");
    }

    public async Task<Dispute> CreateAsync(Dispute dispute)
    {
        await _disputes.InsertOneAsync(dispute);
        return dispute;
    }

    public async Task<Dispute?> GetByIdAsync(string id)
    {
        return await _disputes.Find(d => d.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Dispute?> GetByOrderIdAsync(string orderId)
    {
        return await _disputes.Find(d => d.OrderId == orderId).FirstOrDefaultAsync();
    }

    public async Task<List<Dispute>> GetByUserIdAsync(string userId)
    {
        var filter = Builders<Dispute>.Filter.Or(
            Builders<Dispute>.Filter.Eq(d => d.BuyerId, userId),
            Builders<Dispute>.Filter.Eq(d => d.SellerId, userId)
        );
        return await _disputes.Find(filter)
            .SortByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Dispute>> GetAllAsync(DisputeStatus? status = null, int page = 1, int pageSize = 20)
    {
        var filter = status.HasValue
            ? Builders<Dispute>.Filter.Eq(d => d.Status, status.Value)
            : Builders<Dispute>.Filter.Empty;

        return await _disputes.Find(filter)
            .SortByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();
    }

    public async Task<long> GetCountAsync(DisputeStatus? status = null)
    {
        var filter = status.HasValue
            ? Builders<Dispute>.Filter.Eq(d => d.Status, status.Value)
            : Builders<Dispute>.Filter.Empty;

        return await _disputes.CountDocumentsAsync(filter);
    }

    public async Task UpdateAsync(string id, Dispute dispute)
    {
        dispute.UpdatedAt = DateTime.UtcNow;
        await _disputes.ReplaceOneAsync(d => d.Id == id, dispute);
    }

    public async Task AddMessageAsync(string disputeId, DisputeMessage message)
    {
        var update = Builders<Dispute>.Update
            .Push(d => d.Messages, message)
            .Set(d => d.UpdatedAt, DateTime.UtcNow);

        await _disputes.UpdateOneAsync(d => d.Id == disputeId, update);
    }
}
