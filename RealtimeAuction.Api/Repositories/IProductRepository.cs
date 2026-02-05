using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(string id);
    Task<Product> CreateAsync(Product product);
    Task<Product> UpdateAsync(Product product);
    Task<bool> DeleteAsync(string id);
    Task<List<Product>> GetAllAsync();
}
