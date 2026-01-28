using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models
{
    public class Order
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string AuctionId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string SellerId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string BuyerId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal Amount { get; set; }

        [BsonRepresentation(BsonType.String)]
        public EscrowStatus EscrowStatus { get; set; } = EscrowStatus.MoneyHeld;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum EscrowStatus
    {
        MoneyHeld,
        Released,
        Refunded
    }
}
