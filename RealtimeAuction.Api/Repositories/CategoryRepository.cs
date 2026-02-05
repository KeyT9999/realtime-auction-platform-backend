using MongoDB.Driver;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly IMongoCollection<Category> _categories;

    public CategoryRepository(IMongoDatabase database)
    {
        _categories = database.GetCollection<Category>("Categories");
    }

    public async Task<Category?> GetByIdAsync(string id)
    {
        return await _categories.Find(c => c.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Category> CreateAsync(Category category)
    {
        category.CreatedAt = DateTime.UtcNow;
        category.UpdatedAt = DateTime.UtcNow;
        await _categories.InsertOneAsync(category);
        return category;
    }

    public async Task<Category> UpdateAsync(Category category)
    {
        category.UpdatedAt = DateTime.UtcNow;
        await _categories.ReplaceOneAsync(c => c.Id == category.Id, category);
        return category;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _categories.DeleteOneAsync(c => c.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<Category>> GetByParentIdAsync(string? parentCategoryId)
    {
        if (parentCategoryId == null)
        {
            return await GetRootCategoriesAsync();
        }
        return await _categories.Find(c => c.ParentCategoryId == parentCategoryId).ToListAsync();
    }

    public async Task<List<Category>> GetRootCategoriesAsync()
    {
        return await _categories.Find(c => c.ParentCategoryId == null).ToListAsync();
    }

    public async Task<List<Category>> GetAllAsync()
    {
        return await _categories.Find(_ => true).ToListAsync();
    }
}
