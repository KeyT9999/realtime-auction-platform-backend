using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Product;

public class ProductResponseDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ProductCondition Condition { get; set; }
    public string? Category { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public int? Year { get; set; }
    public List<string> Images { get; set; } = new();
    public string? Specifications { get; set; }
    public bool IsOriginalOwner { get; set; }
    public bool AllowReturn { get; set; }
    public string? AdditionalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
