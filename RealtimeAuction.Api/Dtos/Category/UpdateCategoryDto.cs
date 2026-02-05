using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Category;

public class UpdateCategoryDto
{
    [MinLength(2)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? ParentCategoryId { get; set; }
}
