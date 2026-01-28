using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models
{
    public class Auction
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal StartPrice { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal CurrentPrice { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal StepPrice { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        [BsonRepresentation(BsonType.String)]
        public AuctionStatus Status { get; set; } = AuctionStatus.Draft;

        [BsonRepresentation(BsonType.ObjectId)]
        public string SellerId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string? WinnerId { get; set; }

        public List<string> ImageUrls { get; set; } = new();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum AuctionStatus
    {
        Draft,
        Scheduled,
        Active,
        Ended,
        Completed,
        Disputed,
        Expired
    }
}
