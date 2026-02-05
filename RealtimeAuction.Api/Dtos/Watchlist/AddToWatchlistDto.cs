using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Watchlist;

public class AddToWatchlistDto
{
    [Required]
    public string AuctionId { get; set; } = null!;
}
