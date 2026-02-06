using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Auction;

public class UpdateAuctionDto
{
    [MinLength(3)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Starting price must be greater than 0")]
    public decimal? StartingPrice { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Reserve price must be greater than 0")]
    public decimal? ReservePrice { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Duration must be at least 1 minute")]
    public int? Duration { get; set; }

    public string? CategoryId { get; set; }

    public List<string>? Images { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Bid increment must be greater than 0")]
    public decimal? BidIncrement { get; set; }

    public int? AutoExtendDuration { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Buyout price must be greater than 0")]
    public decimal? BuyoutPrice { get; set; }
}
