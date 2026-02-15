using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public class WithdrawalRepository : IWithdrawalRepository
{
    private readonly IMongoCollection<WithdrawalRequest> _withdrawals;

    public WithdrawalRepository(IMongoDatabase database)
    {
        _withdrawals = database.GetCollection<WithdrawalRequest>("WithdrawalRequests");
    }

    public async Task<WithdrawalRequest> CreateAsync(WithdrawalRequest withdrawal)
    {
        await _withdrawals.InsertOneAsync(withdrawal);
        return withdrawal;
    }

    public async Task<WithdrawalRequest?> GetByIdAsync(string id)
    {
        return await _withdrawals.Find(w => w.Id == id).FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(WithdrawalRequest withdrawal)
    {
        withdrawal.UpdatedAt = DateTime.UtcNow;
        await _withdrawals.ReplaceOneAsync(w => w.Id == withdrawal.Id, withdrawal);
    }

    public async Task<List<WithdrawalRequest>> GetByUserIdAsync(string userId)
    {
        return await _withdrawals.Find(w => w.UserId == userId)
            .SortByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<WithdrawalRequest>> GetByStatusAsync(WithdrawalStatus status)
    {
        return await _withdrawals.Find(w => w.Status == status)
            .SortByDescending(w => w.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<WithdrawalRequest>> GetByUserIdAndDateRangeAsync(string userId, DateTime from, DateTime to)
    {
        return await _withdrawals.Find(w => 
            w.UserId == userId && 
            w.CreatedAt >= from && 
            w.CreatedAt <= to &&
            w.Status != WithdrawalStatus.Cancelled &&
            w.Status != WithdrawalStatus.Rejected)
            .ToListAsync();
    }

    public async Task<List<WithdrawalRequest>> GetAllAsync()
    {
        return await _withdrawals.Find(_ => true)
            .SortByDescending(w => w.CreatedAt)
            .ToListAsync();
    }
}
