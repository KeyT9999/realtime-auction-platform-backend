namespace RealtimeAuction.Api.Dtos.Category;

public class CategoryResponseDto
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public List<CategoryResponseDto>? Children { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
