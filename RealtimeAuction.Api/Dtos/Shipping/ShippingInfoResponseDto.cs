using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Shipping;

public class ShippingInfoResponseDto
{
    public string Id { get; set; } = null!;
    public string AuctionId { get; set; } = null!;
    public string Province { get; set; } = null!;
    public ShippingFeeType FeeType { get; set; }
    public decimal? ShippingFee { get; set; }
    public ShippingMethod Method { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
