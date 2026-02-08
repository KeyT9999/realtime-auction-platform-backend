using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models;

public class Review
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string OrderId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string ReviewerId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string RevieweeId { get; set; } = null!;

    public int Rating { get; set; } // 1-5 stars

    public string? Comment { get; set; } // Max 500 chars

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
