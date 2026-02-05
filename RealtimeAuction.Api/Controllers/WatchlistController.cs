using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Watchlist;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/watchlist")]
[Authorize]
public class WatchlistController : ControllerBase
{
    private readonly IWatchlistRepository _watchlistRepository;
    private readonly IAuctionRepository _auctionRepository;
    private readonly ILogger<WatchlistController> _logger;

    public WatchlistController(
        IWatchlistRepository watchlistRepository,
        IAuctionRepository auctionRepository,
        ILogger<WatchlistController> logger)
    {
        _watchlistRepository = watchlistRepository;
        _auctionRepository = auctionRepository;
        _logger = logger;
    }

    [HttpGet("my-watchlist")]
    public async Task<IActionResult> GetMyWatchlist()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var watchlists = await _watchlistRepository.GetByUserIdAsync(userId);
            var response = new List<WatchlistResponseDto>();

            foreach (var watchlist in watchlists)
            {
                var auction = await _auctionRepository.GetByIdAsync(watchlist.AuctionId);
                var watchlistDto = new WatchlistResponseDto
                {
                    Id = watchlist.Id ?? "",
                    UserId = watchlist.UserId,
                    AuctionId = watchlist.AuctionId,
                    CreatedAt = watchlist.CreatedAt
                };

                if (auction != null)
                {
                    watchlistDto.Auction = new AuctionSummaryDto
                    {
                        Id = auction.Id ?? "",
                        Title = auction.Title,
                        CurrentPrice = auction.CurrentPrice,
                        EndTime = auction.EndTime,
                        ImageUrl = auction.Images.FirstOrDefault()
                    };
                }

                response.Add(watchlistDto);
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting my watchlist");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> AddToWatchlist([FromBody] AddToWatchlistDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var auction = await _auctionRepository.GetByIdAsync(request.AuctionId);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            var watchlist = new Watchlist
            {
                UserId = userId,
                AuctionId = request.AuctionId
            };

            var created = await _watchlistRepository.AddAsync(watchlist);
            var response = new WatchlistResponseDto
            {
                Id = created.Id ?? "",
                UserId = created.UserId,
                AuctionId = created.AuctionId,
                CreatedAt = created.CreatedAt
            };

            if (auction != null)
            {
                response.Auction = new AuctionSummaryDto
                {
                    Id = auction.Id ?? "",
                    Title = auction.Title,
                    CurrentPrice = auction.CurrentPrice,
                    EndTime = auction.EndTime,
                    ImageUrl = auction.Images.FirstOrDefault()
                };
            }

            return CreatedAtAction(nameof(GetMyWatchlist), response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding to watchlist");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveFromWatchlist(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var watchlist = await _watchlistRepository.GetByIdAsync(id);
            if (watchlist == null)
            {
                return NotFound(new { message = "Watchlist item not found" });
            }

            // User can only remove their own watchlist items
            if (watchlist.UserId != userId)
            {
                return Forbid();
            }

            var result = await _watchlistRepository.RemoveAsync(id);
            if (result)
            {
                return Ok(new { message = "Removed from watchlist successfully" });
            }
            return NotFound(new { message = "Watchlist item not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing from watchlist");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("stats")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetWatchlistStats()
    {
        try
        {
            var allWatchlists = await _watchlistRepository.GetAllAsync();
            var now = DateTime.UtcNow;
            var today = now.Date;
            var weekStart = today.AddDays(-(int)now.DayOfWeek);

            var stats = new WatchlistStatsDto
            {
                TotalWatchlists = allWatchlists.Count,
                UniqueUsers = allWatchlists.Select(w => w.UserId).Distinct().Count(),
                UniqueAuctions = allWatchlists.Select(w => w.AuctionId).Distinct().Count(),
                WatchlistsToday = allWatchlists.Count(w => w.CreatedAt >= today),
                WatchlistsThisWeek = allWatchlists.Count(w => w.CreatedAt >= weekStart)
            };
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting watchlist stats");
            return BadRequest(new { message = ex.Message });
        }
    }
}
