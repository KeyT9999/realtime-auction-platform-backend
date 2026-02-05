using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Bid;

public class AutoBidSettingsDto
{
    [Range(0.01, double.MaxValue, ErrorMessage = "Max bid must be greater than 0")]
    public decimal MaxBid { get; set; }

    public bool IsActive { get; set; } = false;
}

