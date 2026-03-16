using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public interface ITransactionRepository
{
    Task<Transaction> CreateAsync(Transaction transaction);
    Task<Transaction> UpdateAsync(Transaction transaction);
    Task<List<Transaction>> GetAllAsync();
    Task<List<Transaction>> GetByUserIdAsync(string userId);
    Task<List<Transaction>> GetByUserIdAndTypeAsync(string userId, TransactionType type);
    Task<Transaction?> GetByIdAsync(string id);
    Task<List<Transaction>> GetByPayOsOrderCodeAsync(long orderCode);
    Task<List<Transaction>> GetByAuctionIdAsync(string auctionId);
    Task<(List<Transaction> Items, int TotalCount)> GetPagedByUserIdAsync(
        string userId,
        int page,
        int limit,
        TransactionType? type = null,
        DateTime? dateFrom = null,
        DateTime? dateTo = null);
}
