using RealtimeAuction.Api.Dtos;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public async Task<List<CategoryDto>> GetAllAsync()
        {
            var categories = await _categoryRepository.GetAllAsync();
            return BuildCategoryTree(categories, null);
        }

        public async Task<Category> CreateAsync(CreateCategoryDto dto)
        {
            var category = new Category
            {
                Name = dto.Name,
                Slug = dto.Name.ToLower().Replace(" ", "-"), // Simple slug generation
                ParentId = dto.ParentId,
                Attributes = dto.Attributes?.Select(a => new CategoryAttribute
                {
                    Name = a.Name,
                    Type = a.Type,
                    Options = a.Options,
                    IsRequired = a.IsRequired
                }).ToList() ?? new List<CategoryAttribute>()
            };

            return await _categoryRepository.CreateAsync(category);
        }

        public async Task<Category?> UpdateAsync(string id, CreateCategoryDto dto)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return null;

            category.Name = dto.Name;
            category.Slug = dto.Name.ToLower().Replace(" ", "-");
            category.ParentId = dto.ParentId;
            category.Attributes = dto.Attributes?.Select(a => new CategoryAttribute
            {
                Name = a.Name,
                Type = a.Type,
                Options = a.Options,
                IsRequired = a.IsRequired
            }).ToList() ?? new List<CategoryAttribute>();
            category.UpdatedAt = DateTime.UtcNow;

            return await _categoryRepository.UpdateAsync(category);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null) return false;

            // Optional: Check if children exist
            // var all = await _categoryRepository.GetAllAsync();
            // if (all.Any(c => c.ParentId == id)) throw new InvalidOperationException("Cannot delete category with children");

            await _categoryRepository.DeleteAsync(id);
            return true;
        }

        private List<CategoryDto> BuildCategoryTree(List<Category> allCategories, string? parentId)
        {
            return allCategories
                .Where(c => c.ParentId == parentId)
                .Select(c => new CategoryDto
                {
                    Id = c.Id!,
                    Name = c.Name,
                    Slug = c.Slug,
                    ParentId = c.ParentId,
                    Attributes = c.Attributes.Select(a => new CategoryAttributeDto
                    {
                        Name = a.Name,
                        Type = a.Type,
                        Options = a.Options,
                        IsRequired = a.IsRequired
                    }).ToList(),
                    Children = BuildCategoryTree(allCategories, c.Id)
                })
                .ToList();
        }
    }
}
