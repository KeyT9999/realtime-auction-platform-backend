using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models;

public class OtpToken
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string OtpCodeHash { get; set; } = null!; // OTP đã hash bằng BCrypt

    public string UserId { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; }

    public int Attempts { get; set; } = 0; // Số lần nhập sai

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
