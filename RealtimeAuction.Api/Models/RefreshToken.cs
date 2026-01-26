using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models
{
    public class RefreshToken
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Token { get; set; } = null!;

        public string UserId { get; set; } = null!;

        public DateTime ExpiresAt { get; set; }

        public bool Revoked { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
