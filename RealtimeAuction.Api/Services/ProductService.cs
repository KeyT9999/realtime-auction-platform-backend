using System.Text.Json;
using RealtimeAuction.Api.Dtos;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;
        private readonly IWebHostEnvironment _environment;

        public ProductService(IProductRepository productRepository, IWebHostEnvironment environment)
        {
            _productRepository = productRepository;
            _environment = environment;
        }

        public async Task<Product> CreateAsync(CreateProductDto dto, string sellerId)
        {
            var imageUrls = new List<string>();
            var uploadsFolder = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads");
            
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            foreach (var file in dto.Images)
            {
                if (file.Length > 0)
                {
                    var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    
                    // Assume api serves from /uploads/filename
                    imageUrls.Add($"/uploads/{fileName}");
                }
            }

            var attributes = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(dto.AttributesJson))
            {
                try
                {
                    attributes = JsonSerializer.Deserialize<Dictionary<string, object>>(dto.AttributesJson) ?? new Dictionary<string, object>();
                }
                catch
                {
                    // Ignore parsing error or handle it
                }
            }

            var product = new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                CategoryId = dto.CategoryId,
                SellerId = sellerId,
                Images = imageUrls,
                Status = "Pending",
                Attributes = attributes
            };

            return await _productRepository.CreateAsync(product);
        }

        public async Task<Product?> ApproveAsync(string id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null) return null;

            product.Status = "Active";
            return await _productRepository.UpdateAsync(product);
        }

        public async Task<List<Product>> SearchAsync(ProductFilterDto filter)
        {
            return await _productRepository.SearchAsync(filter);
        }
    }
}
