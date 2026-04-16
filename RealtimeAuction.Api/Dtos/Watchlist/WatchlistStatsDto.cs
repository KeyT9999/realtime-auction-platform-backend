// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho WatchlistStatsDto.
namespace RealtimeAuction.Api.Dtos.Watchlist;

public class WatchlistStatsDto
{
    public int TotalWatchlists { get; set; }
    public int UniqueUsers { get; set; }
    public int UniqueAuctions { get; set; }
    public int WatchlistsToday { get; set; }
    public int WatchlistsThisWeek { get; set; }
}
