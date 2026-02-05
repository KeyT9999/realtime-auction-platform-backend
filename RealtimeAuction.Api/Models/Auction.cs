using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Models;

public class Auction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal StartingPrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal CurrentPrice { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? ReservePrice { get; set; } // Optional - does not enforce minimum sale price

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public int Duration { get; set; } // Stored in minutes, can be updated (e.g., when auto-extend occurs)

    [BsonRepresentation(BsonType.Int32)]
    public AuctionStatus Status { get; set; } = AuctionStatus.Draft;

    [BsonRepresentation(BsonType.ObjectId)]
    public string SellerId { get; set; } = null!; // Reference to User

    [BsonRepresentation(BsonType.ObjectId)]
    public string CategoryId { get; set; } = null!; // Reference to Category

    [BsonRepresentation(BsonType.ObjectId)]
    public string ProductId { get; set; } = null!; // Reference to Product (one-to-one relationship)

    public List<string> Images { get; set; } = new(); // URLs to cloud storage

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal BidIncrement { get; set; } // Minimum amount to increase bid

    public int? AutoExtendDuration { get; set; } // Duration in minutes to extend auction (stored but logic not implemented)

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
