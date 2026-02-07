using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Models;

public class Transaction
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string UserId { get; set; } = null!;

    [BsonRepresentation(BsonType.Int32)]
    public TransactionType Type { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal Amount { get; set; }

    public string? Description { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? RelatedAuctionId { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? RelatedBidId { get; set; }

    public long? PayOsOrderCode { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal BalanceBefore { get; set; }

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal BalanceAfter { get; set; }

    [BsonRepresentation(BsonType.Int32)]
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    public bool BuyerConfirmed { get; set; } = false;

    public bool SellerConfirmed { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
