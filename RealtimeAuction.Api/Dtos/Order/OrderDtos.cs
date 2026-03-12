using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Order;

public class OrderDto
{
    public string Id { get; set; } = null!;
    public string AuctionId { get; set; } = null!;
    public string BuyerId { get; set; } = null!;
    public string SellerId { get; set; } = null!;
    public string? BuyerName { get; set; }
    public string? SellerName { get; set; }
    public decimal Amount { get; set; }
    public OrderStatus Status { get; set; }
    public string StatusText { get; set; } = null!;
    public string? TrackingNumber { get; set; }
    public string? ShippingCarrier { get; set; }
    public string? ShippingNote { get; set; }
    public string ProductTitle { get; set; } = null!;
    public string? ProductImage { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Review status
    public bool BuyerHasReviewed { get; set; }
    public bool SellerHasReviewed { get; set; }
    public bool CanReview { get; set; } // Calculated based on current user

    // === ESCROW INFO ===
    public decimal EscrowAmount { get; set; }
    public DateTime? EscrowFrozenAt { get; set; }
    public DateTime? EscrowAutoReleaseAt { get; set; }
    public DateTime? EscrowReleasedAt { get; set; }
    public string? EscrowReleaseReason { get; set; }
    /// <summary>"Frozen" | "Released" | "Refunded" | "None"</summary>
    public string EscrowStatus { get; set; } = "None";
    /// <summary>Số ngày còn lại đến auto-release (null nếu đã release hoặc chưa shipped)</summary>
    public int? DaysUntilAutoRelease { get; set; }
}


public class ShipOrderRequest
{
    public string? TrackingNumber { get; set; }
    public string? ShippingCarrier { get; set; }
    public string? ShippingNote { get; set; }
}

public class CancelOrderRequest
{
    public string? Reason { get; set; }
}

public class OrderResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public OrderDto? Order { get; set; }
}
