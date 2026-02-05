using System.ComponentModel.DataAnnotations;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Product;

public class CreateProductDto
{
    [Required]
    [MinLength(2)]
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    public ProductCondition Condition { get; set; } = ProductCondition.New;

    public string? Category { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    [Range(1900, 2100)]
    public int? Year { get; set; }

    public List<string> Images { get; set; } = new();

    public string? Specifications { get; set; } // JSON string

    public bool IsOriginalOwner { get; set; } = false; // Cam kết chính chủ

    public bool AllowReturn { get; set; } = false; // Cho phép hoàn trả

    public string? AdditionalNotes { get; set; } // Ghi chú thêm
}
