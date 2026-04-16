// Mục đích tệp: Truy cap va thao tac du lieu cho phan IBankAccountRepository.
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IBankAccountRepository
{
    Task<BankAccount> CreateAsync(BankAccount bankAccount);
    Task<BankAccount?> GetByIdAsync(string id);
    Task UpdateAsync(BankAccount bankAccount);
    Task DeleteAsync(string id);
    Task<List<BankAccount>> GetByUserIdAsync(string userId);
    Task<BankAccount?> GetDefaultByUserIdAsync(string userId);
    Task ClearDefaultByUserIdAsync(string userId);
}
