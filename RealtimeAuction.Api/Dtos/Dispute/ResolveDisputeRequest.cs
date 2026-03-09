using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Dispute;

public class ResolveDisputeRequest
{
    /// <summary>BuyerWins or SellerWins</summary>
    public DisputeStatus Resolution { get; set; }
    public string AdminNote { get; set; } = null!;
    public string? ResolutionDetail { get; set; }
}
