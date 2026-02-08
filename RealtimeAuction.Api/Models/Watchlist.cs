using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models;

public class Watchlist
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!; // Reference to User

    [BsonRepresentation(BsonType.ObjectId)]
    public string AuctionId { get; set; } = null!; // Reference to Auction

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Email notification tracking
    public bool EndingSoonEmailSent { get; set; } = false;
    public DateTime? EndingSoonEmailSentAt { get; set; }
}
