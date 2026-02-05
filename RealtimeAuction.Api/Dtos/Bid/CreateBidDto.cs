using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Bid;

public class CreateBidDto
{
    [Required]
    public string AuctionId { get; set; } = null!;

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Bid amount must be greater than 0")]
    public decimal Amount { get; set; }

    public AutoBidSettingsDto? AutoBid { get; set; }
}

