using System.ComponentModel.DataAnnotations;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Auction;

public class CreateAuctionDto
{
    [Required]
    [MinLength(3)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    [Required]
    [Range(1000, double.MaxValue, ErrorMessage = "Starting price must be at least 1,000 VND")]
    public decimal StartingPrice { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Reserve price must be greater than 0")]
    public decimal? ReservePrice { get; set; }

    [Required]
    public DateTime StartTime { get; set; }

    [Required]
    public DateTime EndTime { get; set; }

    [Required]
    [Range(60, int.MaxValue, ErrorMessage = "Duration must be at least 60 minutes (1 hour)")]
    public int Duration { get; set; }

    [Required]
    public string CategoryId { get; set; } = null!;

    [Required]
    public string ProductId { get; set; } = null!;

    [Required]
    [MinLength(1, ErrorMessage = "At least 1 image is required")]
    [MaxLength(5, ErrorMessage = "Maximum 5 images allowed")]
    public List<string> Images { get; set; } = new();

    [Required]
    [Range(1000, double.MaxValue, ErrorMessage = "Bid increment must be at least 1,000 VND")]
    public decimal BidIncrement { get; set; }

    public int? AutoExtendDuration { get; set; }
}
