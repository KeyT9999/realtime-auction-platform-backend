using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Email { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public string FullName { get; set; } = null!;

        public string? Role { get; set; } = "User"; // User, Admin

        public bool IsEmailVerified { get; set; } = false;

        public DateTime? EmailVerifiedAt { get; set; }

        public string? Phone { get; set; }

        public string? Address { get; set; }

        public bool IsLocked { get; set; } = false;

        public DateTime? LockedAt { get; set; }

        public string? LockedReason { get; set; }

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal AvailableBalance { get; set; } = 0;

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal EscrowBalance { get; set; } = 0; // Tiền đang hold cho auction (bid, payment)

        [BsonRepresentation(BsonType.Decimal128)]
        public decimal HeldBalance { get; set; } = 0; // Tiền đang hold cho withdrawal request

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
