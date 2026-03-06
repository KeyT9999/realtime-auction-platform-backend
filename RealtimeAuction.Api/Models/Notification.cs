using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models;

public class Notification
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string UserId { get; set; } = null!;
    public string Type { get; set; } = "Info"; // e.g. AuctionApproved, Outbid, OrderShipped
    public string Title { get; set; } = null!;
    public string? Message { get; set; }
    public string? RelatedId { get; set; }  // auctionId, orderId, etc.
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
