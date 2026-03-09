using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Dispute;

public class DisputeMessageDto
{
    public string Id { get; set; } = null!;
    public string SenderId { get; set; } = null!;
    public string SenderName { get; set; } = null!;
    public string SenderRole { get; set; } = null!;
    public string Content { get; set; } = null!;
    public List<string> Attachments { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class DisputeResponseDto
{
    public string Id { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public string AuctionId { get; set; } = null!;
    public string BuyerId { get; set; } = null!;
    public string SellerId { get; set; } = null!;
    public string OpenedBy { get; set; } = null!;
    public DisputeReason Reason { get; set; }
    public string Description { get; set; } = null!;
    public List<string> EvidenceImages { get; set; } = new();
    public DisputeStatus Status { get; set; }

    // Admin
    public string? AdminId { get; set; }
    public string? AdminNote { get; set; }
    public string? Resolution { get; set; }

    // Display
    public string? ProductTitle { get; set; }
    public string? ProductImage { get; set; }
    public string? BuyerName { get; set; }
    public string? SellerName { get; set; }

    // Messages
    public List<DisputeMessageDto> Messages { get; set; } = new();

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
