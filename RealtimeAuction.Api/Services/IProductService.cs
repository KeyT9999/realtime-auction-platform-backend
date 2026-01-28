using RealtimeAuction.Api.Dtos;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services
{
    public interface IProductService
    {
        Task<Product> CreateAsync(CreateProductDto dto, string sellerId);
        Task<Product?> ApproveAsync(string id);
        Task<List<Product>> SearchAsync(ProductFilterDto filter);
    }
}
