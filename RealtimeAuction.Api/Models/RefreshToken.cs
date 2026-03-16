using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models
{
    public class RefreshToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // Legacy plaintext token kept only for backward compatibility with old sessions.
        public string? Token { get; set; }

        public string? TokenHash { get; set; }

        [BsonIgnore]
        public string PlainTextToken { get; set; } = string.Empty;

        public string UserId { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }

        public bool Revoked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
