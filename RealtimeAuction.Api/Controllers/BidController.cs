using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RealtimeAuction.Api.Dtos.Bid;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using RealtimeAuction.Api.Hubs;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/bids")]
[Authorize]
public class BidController : ControllerBase
{
    private readonly IBidRepository _bidRepository;
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBidService _bidService;
    private readonly IHubContext<AuctionHub> _hubContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<BidController> _logger;

    public BidController(
        IBidRepository bidRepository,
        IAuctionRepository auctionRepository,
        IUserRepository userRepository,
        IBidService bidService,
        IHubContext<AuctionHub> hubContext,
        IEmailService emailService,
        ILogger<BidController> logger)
    {
        _bidRepository = bidRepository;
        _auctionRepository = auctionRepository;
        _userRepository = userRepository;
        _bidService = bidService;
        _hubContext = hubContext;
        _emailService = emailService;
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

            // Sử dụng BidService để xử lý logic hold balance
            var result = await _bidService.PlaceBidAsync(userId, request);
            
            if (!result.Success)
            {
                return BadRequest(new { message = result.ErrorMessage });
            }

            var auction = await _auctionRepository.GetByIdAsync(request.AuctionId);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            // Get user info for the bid
            var user = await _userRepository.GetByIdAsync(userId);
            var userName = user?.FullName ?? "Anonymous";

            var response = new BidResponseDto
            {
                Id = result.Bid?.Id ?? "",
                AuctionId = result.Bid?.AuctionId ?? request.AuctionId,
                UserId = result.Bid?.UserId ?? userId,
                UserName = userName,
                Amount = result.Bid?.Amount ?? request.Amount,
                Timestamp = result.Bid?.Timestamp ?? DateTime.UtcNow,
                IsWinningBid = result.Bid?.IsWinningBid ?? false,
                AutoBid = result.Bid?.AutoBid != null ? new AutoBidSettingsDto
                {
                    MaxBid = result.Bid.AutoBid.MaxBid,
                    IsActive = result.Bid.AutoBid.IsActive
                } : null,
                CreatedAt = result.Bid?.CreatedAt ?? DateTime.UtcNow
            };

            // Get all bids for count
            var allBids = await _bidRepository.GetByAuctionIdAsync(request.AuctionId);

            // Broadcast new bid via SignalR
            await AuctionHub.NotifyNewBid(_hubContext, request.AuctionId, new
            {
                Bid = response,
                AuctionId = request.AuctionId,
                CurrentPrice = request.Amount,
                BidCount = allBids.Count
            });

            // Notify previous winning bidder that they've been outbid
            if (result.OutbidUserId != null)
            {
                var outbidUser = await _userRepository.GetByIdAsync(result.OutbidUserId);
                var outbidBids = await _bidRepository.GetByAuctionIdAsync(request.AuctionId);
                var outbidBid = outbidBids
                    .Where(b => b.UserId == result.OutbidUserId)
                    .OrderByDescending(b => b.Amount)
                    .FirstOrDefault();

                if (outbidBid != null)
                {
                    await AuctionHub.NotifyUserOutbid(_hubContext, result.OutbidUserId, new
                    {
                        AuctionId = request.AuctionId,
                        AuctionTitle = auction.Title,
                        YourBid = outbidBid.Amount,
                        NewBid = request.Amount,
                        BidderName = userName
                    });

                    // Send outbid email notification
                    if (outbidUser != null && !string.IsNullOrEmpty(outbidUser.Email))
                    {
                        try
                        {
                            var suggestedBid = request.Amount + (auction.BidIncrement > 0 ? auction.BidIncrement : 10000);
                            var auctionUrl = $"http://localhost:5173/auctions/{auction.Id}";
                            
                            await _emailService.SendOutbidNotificationEmailAsync(
                                outbidUser.Email,
                                outbidUser.FullName,
                                auction.Title,
                                FormatCurrency(outbidBid.Amount),
                                FormatCurrency(request.Amount),
                                FormatCurrency(suggestedBid),
                                auctionUrl);
                            
                            _logger.LogInformation("Sent outbid email to {Email}", outbidUser.Email);
                        }
                        catch (Exception emailEx)
                        {
                            _logger.LogError(emailEx, "Failed to send outbid email to {Email}", outbidUser.Email);
                        }
                    }
                }
            }

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

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} ₫", amount);
    }
}
