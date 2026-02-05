using Microsoft.AspNetCore.Http; // For IFormFile

namespace RealtimeAuction.Api.Dtos
{
    public class CreateProductDto
    {
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public string CategoryId { get; set; } = null!;
        public List<IFormFile> Images { get; set; } = new();
        public string AttributesJson { get; set; } = "{}"; // JSON string for attributes
    }

    public class ProductDto
    {
        public string Id { get; set; } = null!;
        public string Name { get; set; } = null!;
        public string Description { get; set; } = null!;
        public decimal Price { get; set; }
        public string CategoryId { get; set; } = null!;
        public string SellerId { get; set; } = null!;
        public List<string> Images { get; set; } = new();
        public string Status { get; set; } = null!;
        public Dictionary<string, object> Attributes { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
    
    public class ProductFilterDto
    {
        public string? Keyword { get; set; }
        public string? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? Status { get; set; } // Add Status filter
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}
