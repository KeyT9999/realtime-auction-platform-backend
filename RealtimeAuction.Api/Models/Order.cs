using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Models;

public class Order
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string AuctionId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BuyerId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string SellerId { get; set; } = null!;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    [BsonRepresentation(BsonType.Int32)]
    public OrderStatus Status { get; set; } = OrderStatus.PendingShipment;

    // Thông tin vận chuyển
    public string? TrackingNumber { get; set; }
    public string? ShippingCarrier { get; set; }
    public string? ShippingNote { get; set; }

    // Thông tin sản phẩm (denormalized for display)
    public string ProductTitle { get; set; } = null!;
    public string? ProductImage { get; set; }

    // Timestamps
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? CancelledBy { get; set; }

    // Review status flags
    public bool BuyerHasReviewed { get; set; } = false;
    public bool SellerHasReviewed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
