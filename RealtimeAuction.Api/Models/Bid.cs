using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models;

public class Bid
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string AuctionId { get; set; } = null!; // Reference to Auction

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!; // Reference to User

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsWinningBid { get; set; } = false;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal HeldAmount { get; set; } = 0;  // Số tiền đang hold cho bid này

    public bool IsHoldReleased { get; set; } = false;  // Đã release hold chưa

    public AutoBidSettings? AutoBid { get; set; } // Nested class - structure only, logic not implemented

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Nested class for AutoBid settings
    public class AutoBidSettings
    {
        [BsonRepresentation(BsonType.Decimal128)]
        public decimal MaxBid { get; set; } // Maximum bid amount

        public bool IsActive { get; set; } = false; // Whether auto-bid is active
    }
}
