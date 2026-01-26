using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> ExistsByEmailAsync(string email);
}
