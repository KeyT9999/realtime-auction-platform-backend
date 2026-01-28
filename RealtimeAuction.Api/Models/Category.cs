using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models
{
    public class Category
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Name { get; set; } = null!;
        public string Slug { get; set; } = null!;
        
        [BsonRepresentation(BsonType.ObjectId)]
        public string? ParentId { get; set; }

        public List<CategoryAttribute> Attributes { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    public class CategoryAttribute
    {
        public string Name { get; set; } = null!; // e.g., "Screen Size", "RAM"
        public string Type { get; set; } = "text"; // text, number, select
        public List<string>? Options { get; set; } // For "select" type
        public bool IsRequired { get; set; } = false;
    }
}
