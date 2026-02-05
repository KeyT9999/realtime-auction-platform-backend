using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Category;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/categories")]
public class CategoryController : ControllerBase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<CategoryController> _logger;

    public CategoryController(ICategoryRepository categoryRepository, ILogger<CategoryController> logger)
    {
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var categories = await _categoryRepository.GetAllAsync();
            var response = categories.Select(c => new CategoryResponseDto
            {
                Id = c.Id ?? "",
                Name = c.Name,
                Description = c.Description,
                ParentCategoryId = c.ParentCategoryId,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            }).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCategoryById(string id)
    {
        try
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound(new { message = "Category not found" });
            }

            CategoryResponseDto? parentCategory = null;
            if (!string.IsNullOrEmpty(category.ParentCategoryId))
            {
                var parent = await _categoryRepository.GetByIdAsync(category.ParentCategoryId);
                parentCategory = parent != null ? new CategoryResponseDto
                {
                    Id = parent.Id ?? "",
                    Name = parent.Name,
                    Description = parent.Description
                } : null;
            }

            var response = new CategoryResponseDto
            {
                Id = category.Id ?? "",
                Name = category.Name,
                Description = category.Description,
                ParentCategoryId = category.ParentCategoryId,
                ParentCategoryName = parentCategory?.Name,
                CreatedAt = category.CreatedAt,
                UpdatedAt = category.UpdatedAt
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category by ID");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("tree")]
    public async Task<IActionResult> GetCategoryTree()
    {
        try
        {
            var allCategories = await _categoryRepository.GetAllAsync();
            var rootCategories = allCategories.Where(c => string.IsNullOrEmpty(c.ParentCategoryId)).ToList();

            Func<Category, CategoryResponseDto> BuildTree = null!;
            BuildTree = (category) =>
            {
                var children = allCategories.Where(c => c.ParentCategoryId == category.Id).ToList();
                return new CategoryResponseDto
                {
                    Id = category.Id ?? "",
                    Name = category.Name,
                    Description = category.Description,
                    ParentCategoryId = category.ParentCategoryId,
                    Children = children.Select(BuildTree).ToList(),
                    CreatedAt = category.CreatedAt,
                    UpdatedAt = category.UpdatedAt
                };
            };

            var tree = rootCategories.Select(BuildTree).ToList();
            return Ok(tree);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting category tree");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryDto request)
    {
        try
        {
            var category = new Category
            {
                Name = request.Name,
                Description = request.Description,
                ParentCategoryId = request.ParentCategoryId
            };

            var created = await _categoryRepository.CreateAsync(category);
            var response = new CategoryResponseDto
            {
                Id = created.Id ?? "",
                Name = created.Name,
                Description = created.Description,
                ParentCategoryId = created.ParentCategoryId,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };
            return CreatedAtAction(nameof(GetCategoryById), new { id = response.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating category");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateCategory(string id, [FromBody] UpdateCategoryDto request)
    {
        try
        {
            var category = await _categoryRepository.GetByIdAsync(id);
            if (category == null)
            {
                return NotFound(new { message = "Category not found" });
            }

            if (!string.IsNullOrEmpty(request.Name))
                category.Name = request.Name;
            if (request.Description != null)
                category.Description = request.Description;
            if (request.ParentCategoryId != null)
                category.ParentCategoryId = request.ParentCategoryId;

            var updated = await _categoryRepository.UpdateAsync(category);
            var response = new CategoryResponseDto
            {
                Id = updated.Id ?? "",
                Name = updated.Name,
                Description = updated.Description,
                ParentCategoryId = updated.ParentCategoryId,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating category");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteCategory(string id)
    {
        try
        {
            var result = await _categoryRepository.DeleteAsync(id);
            if (result)
            {
                return Ok(new { message = "Category deleted successfully" });
            }
            return NotFound(new { message = "Category not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting category");
            return BadRequest(new { message = ex.Message });
        }
    }
}
