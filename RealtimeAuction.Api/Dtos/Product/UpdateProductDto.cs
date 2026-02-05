using System.ComponentModel.DataAnnotations;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Product;

public class UpdateProductDto
{
    [MinLength(2)]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public ProductCondition? Condition { get; set; }

    public string? Category { get; set; }

    public string? Brand { get; set; }

    public string? Model { get; set; }

    [Range(1900, 2100)]
    public int? Year { get; set; }

    public List<string>? Images { get; set; }

    public string? Specifications { get; set; }
}
