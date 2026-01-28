using RealtimeAuction.Api.Dtos;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services
{
    public interface ICategoryService
    {
        Task<List<CategoryDto>> GetAllAsync();
        Task<Category> CreateAsync(CreateCategoryDto dto);
        Task<Category?> UpdateAsync(string id, CreateCategoryDto dto);
        Task<bool> DeleteAsync(string id);
    }
}
