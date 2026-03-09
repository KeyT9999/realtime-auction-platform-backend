using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Dispute;

public class CreateDisputeRequest
{
    public string OrderId { get; set; } = null!;
    public DisputeReason Reason { get; set; }
    public string Description { get; set; } = null!;
    public List<string> EvidenceImages { get; set; } = new();
}
