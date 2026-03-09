using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models;

/// <summary>
/// Embedded document inside Dispute for the conversation/message thread.
/// </summary>
public class DisputeMessage
{
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

    [BsonRepresentation(BsonType.ObjectId)]
    public string SenderId { get; set; } = null!;

    public string SenderName { get; set; } = null!;

    /// <summary>Buyer, Seller, or Admin</summary>
    public string SenderRole { get; set; } = null!;

    public string Content { get; set; } = null!;

    public List<string> Attachments { get; set; } = new();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
