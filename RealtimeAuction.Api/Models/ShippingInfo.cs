using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Models;

public class ShippingInfo
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string AuctionId { get; set; } = null!;

    public string Province { get; set; } = null!; // Tỉnh/Thành phố

    [BsonRepresentation(BsonType.Int32)]
    public ShippingFeeType FeeType { get; set; } // Người mua/Người bán/Thỏa thuận

    [BsonRepresentation(BsonType.Decimal128)]
    public decimal? ShippingFee { get; set; } // Phí cụ thể (nếu có)

    [BsonRepresentation(BsonType.Int32)]
    public ShippingMethod Method { get; set; } // Gặp trực tiếp/Giao hàng/COD

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
