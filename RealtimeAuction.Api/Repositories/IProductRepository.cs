using RealtimeAuction.Api.Dtos;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> GetByIdAsync(string id);
        Task<Product> CreateAsync(Product product);
        Task<Product> UpdateAsync(Product product);
        Task<List<Product>> SearchAsync(ProductFilterDto filter);
    }
}
