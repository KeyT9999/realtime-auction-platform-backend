using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace RealtimeAuction.Api.Models;

public class PasswordResetOtp
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }
    
    [BsonElement("otpCodeHash")]
    public string OtpCodeHash { get; set; } = null!;
    
    [BsonElement("email")]
    public string Email { get; set; } = null!;
    
    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; }
    
    [BsonElement("isUsed")]
    public bool IsUsed { get; set; } = false;
    
    [BsonElement("attempts")]
    public int Attempts { get; set; } = 0;
    
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
