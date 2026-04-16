// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho AddToWatchlistDto.
using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Watchlist;

public class AddToWatchlistDto
{
    [Required]
    public string AuctionId { get; set; } = null!;
}
