using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(string id);
    Task<Category> CreateAsync(Category category);
    Task<Category> UpdateAsync(Category category);
    Task<bool> DeleteAsync(string id);
    Task<List<Category>> GetByParentIdAsync(string? parentCategoryId);
    Task<List<Category>> GetRootCategoriesAsync();
    Task<List<Category>> GetAllAsync();
}
