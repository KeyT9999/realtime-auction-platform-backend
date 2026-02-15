using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Models;

public class WithdrawalRequest
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.ObjectId)]
    public string BankAccountId { get; set; } = null!;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal ProcessingFee { get; set; } = 0;

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal FinalAmount { get; set; } // Amount - ProcessingFee

    [BsonRepresentation(BsonType.Int32)]
    public WithdrawalStatus Status { get; set; } = WithdrawalStatus.Pending;

    // OTP - lưu hash SHA256, không lưu plain text
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public int OtpAttempts { get; set; } = 0;
    public bool IsOtpVerified { get; set; } = false;
    public DateTime? OtpVerifiedAt { get; set; }

    // Admin actions
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }

    // Complete
    public string? TransactionCode { get; set; }
    public string? TransactionProof { get; set; } // Cloudinary URL
    public DateTime? CompletedAt { get; set; }

    // Cancel
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    // Related transaction
    [BsonRepresentation(BsonType.ObjectId)]
    public string? RelatedTransactionId { get; set; }

    // Bank account snapshot (for audit - denormalized)
    public BankAccountSnapshot? BankSnapshot { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class BankAccountSnapshot
{
    public string BankName { get; set; } = null!;
    public string AccountNumberLast4 { get; set; } = null!; // Chỉ lưu 4 số cuối
    public string AccountName { get; set; } = null!;
}
