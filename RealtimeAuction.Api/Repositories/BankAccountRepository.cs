using MongoDB.Driver;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public class BankAccountRepository : IBankAccountRepository
{
    private readonly IMongoCollection<BankAccount> _bankAccounts;

    public BankAccountRepository(IMongoDatabase database)
    {
        _bankAccounts = database.GetCollection<BankAccount>("BankAccounts");
    }

    public async Task<BankAccount> CreateAsync(BankAccount bankAccount)
    {
        await _bankAccounts.InsertOneAsync(bankAccount);
        return bankAccount;
    }

    public async Task<BankAccount?> GetByIdAsync(string id)
    {
        return await _bankAccounts.Find(b => b.Id == id).FirstOrDefaultAsync();
    }

    public async Task UpdateAsync(BankAccount bankAccount)
    {
        bankAccount.UpdatedAt = DateTime.UtcNow;
        await _bankAccounts.ReplaceOneAsync(b => b.Id == bankAccount.Id, bankAccount);
    }

    public async Task DeleteAsync(string id)
    {
        await _bankAccounts.DeleteOneAsync(b => b.Id == id);
    }

    public async Task<List<BankAccount>> GetByUserIdAsync(string userId)
    {
        return await _bankAccounts.Find(b => b.UserId == userId)
            .SortByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync();
    }

    public async Task<BankAccount?> GetDefaultByUserIdAsync(string userId)
    {
        return await _bankAccounts.Find(b => b.UserId == userId && b.IsDefault)
            .FirstOrDefaultAsync();
    }

    public async Task ClearDefaultByUserIdAsync(string userId)
    {
        var update = Builders<BankAccount>.Update.Set(b => b.IsDefault, false);
        await _bankAccounts.UpdateManyAsync(b => b.UserId == userId && b.IsDefault, update);
    }
}
