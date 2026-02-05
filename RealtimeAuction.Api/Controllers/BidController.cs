using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Bid;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/bids")]
[Authorize]
public class BidController : ControllerBase
{
    private readonly IBidRepository _bidRepository;
    private readonly IAuctionRepository _auctionRepository;
    private readonly ILogger<BidController> _logger;

    public BidController(
        IBidRepository bidRepository,
        IAuctionRepository auctionRepository,
        ILogger<BidController> logger)
    {
        _bidRepository = bidRepository;
        _auctionRepository = auctionRepository;
        _logger = logger;
    }

    [HttpGet("auction/{auctionId}")]
    public async Task<IActionResult> GetBidsByAuction(string auctionId)
    {
        try
        {
            var bids = await _bidRepository.GetByAuctionIdAsync(auctionId);
            var response = bids.Select(b => new BidResponseDto
            {
                Id = b.Id ?? "",
                AuctionId = b.AuctionId,
                UserId = b.UserId,
                Amount = b.Amount,
                Timestamp = b.Timestamp,
                IsWinningBid = b.IsWinningBid,
                AutoBid = b.AutoBid != null ? new AutoBidSettingsDto
                {
                    MaxBid = b.AutoBid.MaxBid,
                    IsActive = b.AutoBid.IsActive
                } : null,
                CreatedAt = b.CreatedAt
            }).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bids by auction");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("my-bids")]
    public async Task<IActionResult> GetMyBids()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var bids = await _bidRepository.GetByUserIdAsync(userId);
            var response = bids.Select(b => new BidResponseDto
            {
                Id = b.Id ?? "",
                AuctionId = b.AuctionId,
                UserId = b.UserId,
                Amount = b.Amount,
                Timestamp = b.Timestamp,
                IsWinningBid = b.IsWinningBid,
                AutoBid = b.AutoBid != null ? new AutoBidSettingsDto
                {
                    MaxBid = b.AutoBid.MaxBid,
                    IsActive = b.AutoBid.IsActive
                } : null,
                CreatedAt = b.CreatedAt
            }).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting my bids");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateBid([FromBody] CreateBidDto request)
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

            if (auction.Status != Models.Enums.AuctionStatus.Active)
            {
                return BadRequest(new { message = "Auction is not active" });
            }

            if (auction.SellerId == userId)
            {
                return BadRequest(new { message = "You cannot bid on your own auction" });
            }

            var bid = new Bid
            {
                AuctionId = request.AuctionId,
                UserId = userId,
                Amount = request.Amount,
                AutoBid = request.AutoBid != null ? new Bid.AutoBidSettings
                {
                    MaxBid = request.AutoBid.MaxBid,
                    IsActive = request.AutoBid.IsActive
                } : null
            };

            var created = await _bidRepository.CreateAsync(bid, auction.CurrentPrice, auction.BidIncrement);
            
            // Update auction current price
            await _auctionRepository.UpdateCurrentPriceAsync(request.AuctionId, request.Amount);

            var response = new BidResponseDto
            {
                Id = created.Id ?? "",
                AuctionId = created.AuctionId,
                UserId = created.UserId,
                Amount = created.Amount,
                Timestamp = created.Timestamp,
                IsWinningBid = created.IsWinningBid,
                AutoBid = created.AutoBid != null ? new AutoBidSettingsDto
                {
                    MaxBid = created.AutoBid.MaxBid,
                    IsActive = created.AutoBid.IsActive
                } : null,
                CreatedAt = created.CreatedAt
            };
            return CreatedAtAction(nameof(GetBidsByAuction), new { auctionId = request.AuctionId }, response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid bid amount");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bid");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> DeleteBid(string id)
    {
        try
        {
            var bid = await _bidRepository.GetByIdAsync(id);
            if (bid == null)
            {
                return NotFound(new { message = "Bid not found" });
            }

            // Note: In a real system, you might want to check if this is the highest bid
            // and handle auction price updates accordingly
            // For now, we'll just delete it
            
            // This would require adding DeleteAsync to IBidRepository
            // For now, returning not implemented
            return StatusCode(501, new { message = "Delete bid not yet implemented" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bid");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("stats")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetBidStats()
    {
        try
        {
            var allBids = await _bidRepository.GetAllAsync();
            var now = DateTime.UtcNow;
            var today = now.Date;
            var weekStart = today.AddDays(-(int)now.DayOfWeek);
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var stats = new BidStatsDto
            {
                TotalBids = allBids.Count,
                HighestBid = allBids.Any() ? allBids.Max(b => b.Amount) : null,
                AverageBid = allBids.Any() ? allBids.Average(b => b.Amount) : null,
                BidsToday = allBids.Count(b => b.CreatedAt >= today),
                BidsThisWeek = allBids.Count(b => b.CreatedAt >= weekStart),
                BidsThisMonth = allBids.Count(b => b.CreatedAt >= monthStart)
            };
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bid stats");
            return BadRequest(new { message = ex.Message });
        }
    }
}
