using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Models;

public class Dispute
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string OrderId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string AuctionId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BuyerId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string SellerId { get; set; } = null!;

    /// <summary>Who opened the dispute: "Buyer" or "Seller"</summary>
    public string OpenedBy { get; set; } = null!;

    [BsonRepresentation(BsonType.Int32)]
    public DisputeReason Reason { get; set; }

    public string Description { get; set; } = null!;

    public List<string> EvidenceImages { get; set; } = new();

    [BsonRepresentation(BsonType.Int32)]
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;

    // Admin handling
    [BsonRepresentation(BsonType.ObjectId)]
    public string? AdminId { get; set; }

    public string? AdminNote { get; set; }
    public string? Resolution { get; set; }

    // Denormalized for display
    public string? ProductTitle { get; set; }
    public string? ProductImage { get; set; }
    public string? BuyerName { get; set; }
    public string? SellerName { get; set; }

    // Conversation thread
    public List<DisputeMessage> Messages { get; set; } = new();

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
