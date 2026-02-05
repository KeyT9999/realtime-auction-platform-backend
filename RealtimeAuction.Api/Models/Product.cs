using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Models;

public class Product
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.Int32)]
    public ProductCondition Condition { get; set; } = ProductCondition.New;

    public string? Category { get; set; } // Category name (not reference to Category model)

    public string? Brand { get; set; }

    public string? Model { get; set; }

    public int? Year { get; set; }

    public List<string> Images { get; set; } = new(); // URLs to cloud storage

    public string? Specifications { get; set; } // JSON string format for flexibility

    public bool IsOriginalOwner { get; set; } = false; // Cam kết chính chủ

    public bool AllowReturn { get; set; } = false; // Cho phép hoàn trả

    public string? AdditionalNotes { get; set; } // Ghi chú thêm

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
