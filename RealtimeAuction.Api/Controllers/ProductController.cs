using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Product;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductController : ControllerBase
{
    private readonly IProductRepository _productRepository;
    private readonly IImageUploadService _imageUploadService;
    private readonly ILogger<ProductController> _logger;

    public ProductController(
        IProductRepository productRepository,
        IImageUploadService imageUploadService,
        ILogger<ProductController> logger)
    {
        _productRepository = productRepository;
        _imageUploadService = imageUploadService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts()
    {
        try
        {
            var products = await _productRepository.GetAllAsync();
            var response = products.Select(p => new ProductResponseDto
            {
                Id = p.Id ?? "",
                Name = p.Name,
                Description = p.Description,
                Condition = p.Condition,
                Category = p.Category,
                Brand = p.Brand,
                Model = p.Model,
                Year = p.Year,
                Images = p.Images,
                Specifications = p.Specifications,
                IsOriginalOwner = p.IsOriginalOwner,
                AllowReturn = p.AllowReturn,
                AdditionalNotes = p.AdditionalNotes,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting products");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProductById(string id)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            var response = new ProductResponseDto
            {
                Id = product.Id ?? "",
                Name = product.Name,
                Description = product.Description,
                Condition = product.Condition,
                Category = product.Category,
                Brand = product.Brand,
                Model = product.Model,
                Year = product.Year,
                Images = product.Images,
                Specifications = product.Specifications,
                IsOriginalOwner = product.IsOriginalOwner,
                AllowReturn = product.AllowReturn,
                AdditionalNotes = product.AdditionalNotes,
                CreatedAt = product.CreatedAt,
                UpdatedAt = product.UpdatedAt
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting product by ID");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto request)
    {
        try
        {
            var product = new Product
            {
                Name = request.Name,
                Description = request.Description,
                Condition = request.Condition,
                Category = request.Category,
                Brand = request.Brand,
                Model = request.Model,
                Year = request.Year,
                Images = request.Images,
                Specifications = request.Specifications,
                IsOriginalOwner = request.IsOriginalOwner,
                AllowReturn = request.AllowReturn,
                AdditionalNotes = request.AdditionalNotes
            };

            var created = await _productRepository.CreateAsync(product);
            var response = new ProductResponseDto
            {
                Id = created.Id ?? "",
                Name = created.Name,
                Description = created.Description,
                Condition = created.Condition,
                Category = created.Category,
                Brand = created.Brand,
                Model = created.Model,
                Year = created.Year,
                Images = created.Images,
                Specifications = created.Specifications,
                IsOriginalOwner = created.IsOriginalOwner,
                AllowReturn = created.AllowReturn,
                AdditionalNotes = created.AdditionalNotes,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };
            return CreatedAtAction(nameof(GetProductById), new { id = response.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateProduct(string id, [FromBody] UpdateProductDto request)
    {
        try
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound(new { message = "Product not found" });
            }

            if (!string.IsNullOrEmpty(request.Name))
                product.Name = request.Name;
            if (request.Description != null)
                product.Description = request.Description;
            if (request.Condition.HasValue)
                product.Condition = request.Condition.Value;
            if (request.Category != null)
                product.Category = request.Category;
            if (request.Brand != null)
                product.Brand = request.Brand;
            if (request.Model != null)
                product.Model = request.Model;
            if (request.Year.HasValue)
                product.Year = request.Year;
            if (request.Images != null)
                product.Images = request.Images;
            if (request.Specifications != null)
                product.Specifications = request.Specifications;

            var updated = await _productRepository.UpdateAsync(product);
            var response = new ProductResponseDto
            {
                Id = updated.Id ?? "",
                Name = updated.Name,
                Description = updated.Description,
                Condition = updated.Condition,
                Category = updated.Category,
                Brand = updated.Brand,
                Model = updated.Model,
                Year = updated.Year,
                Images = updated.Images,
                Specifications = updated.Specifications,
                IsOriginalOwner = updated.IsOriginalOwner,
                AllowReturn = updated.AllowReturn,
                AdditionalNotes = updated.AdditionalNotes,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating product");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("upload-image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded" });
            }

            // Validate file count (single image)
            if (file.Length > 5 * 1024 * 1024) // 5MB
            {
                return BadRequest(new { message = "File size exceeds 5MB limit" });
            }

            // Copy to MemoryStream to avoid disposal issues
            using var sourceStream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await sourceStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            var url = await _imageUploadService.UploadImageAsync(memoryStream, file.FileName);

            return Ok(new { url });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading image");
            return BadRequest(new { message = "Error uploading image" });
        }
    }

    [HttpPost("upload-images")]
    public async Task<IActionResult> UploadImages(List<IFormFile> files)
    {
        try
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { message = "No files uploaded" });
            }

            if (files.Count > 5)
            {
                return BadRequest(new { message = "Maximum 5 images allowed" });
            }

            if (files.Count < 1)
            {
                return BadRequest(new { message = "At least 1 image is required" });
            }

            var imageData = new List<(Stream stream, string fileName)>();
            foreach (var file in files)
            {
                if (file.Length > 5 * 1024 * 1024) // 5MB
                {
                    return BadRequest(new { message = $"File {file.FileName} exceeds 5MB limit" });
                }
                
                // Copy to MemoryStream to avoid disposal issues
                using var sourceStream = file.OpenReadStream();
                var memoryStream = new MemoryStream();
                await sourceStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;
                imageData.Add((memoryStream, file.FileName));
            }

            var urls = await _imageUploadService.UploadImagesAsync(imageData);

            // Dispose streams
            foreach (var (stream, _) in imageData)
            {
                stream.Dispose();
            }

            return Ok(new { urls });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading images");
            return BadRequest(new { message = "Error uploading images" });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteProduct(string id)
    {
        try
        {
            var result = await _productRepository.DeleteAsync(id);
            if (result)
            {
                return Ok(new { message = "Product deleted successfully" });
            }
            return NotFound(new { message = "Product not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product");
            return BadRequest(new { message = ex.Message });
        }
    }
}
