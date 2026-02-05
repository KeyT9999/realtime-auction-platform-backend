using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Category;

public class CreateCategoryDto
{
    [Required]
    [MinLength(2)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string? ParentCategoryId { get; set; }
}
