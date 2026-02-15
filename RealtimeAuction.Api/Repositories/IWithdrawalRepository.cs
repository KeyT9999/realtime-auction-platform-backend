using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public interface IWithdrawalRepository
{
    Task<WithdrawalRequest> CreateAsync(WithdrawalRequest withdrawal);
    Task<WithdrawalRequest?> GetByIdAsync(string id);
    Task UpdateAsync(WithdrawalRequest withdrawal);
    Task<List<WithdrawalRequest>> GetByUserIdAsync(string userId);
    Task<List<WithdrawalRequest>> GetByStatusAsync(WithdrawalStatus status);
    Task<List<WithdrawalRequest>> GetByUserIdAndDateRangeAsync(string userId, DateTime from, DateTime to);
    Task<List<WithdrawalRequest>> GetAllAsync();
}
