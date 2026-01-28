using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Services;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuctionController : ControllerBase
    {
        private readonly IAuctionService _auctionService;

        public AuctionController(IAuctionService auctionService)
        {
            _auctionService = auctionService;
        }

        [HttpPost("create")]
        [Authorize]
        public async Task<IActionResult> CreateAuction([FromBody] Auction auction)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            auction.SellerId = userId;
            // Validate dates
            if (auction.StartTime < DateTime.UtcNow) return BadRequest("Start time must be in the future");
            if (auction.EndTime <= auction.StartTime) return BadRequest("End time must be after start time");

            var createdAuction = await _auctionService.CreateAuctionAsync(auction);
            return Ok(createdAuction);
        }

        [HttpGet]
        public async Task<IActionResult> GetActiveAuctions()
        {
            var auctions = await _auctionService.GetActiveAuctionsAsync();
            return Ok(auctions);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetAuctionById(string id)
        {
            var auction = await _auctionService.GetAuctionByIdAsync(id);
            if (auction == null) return NotFound();
            return Ok(auction);
        }

        [HttpPost("bid")]
        [Authorize]
        public async Task<IActionResult> PlaceBid([FromBody] BidRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            try
            {
                await _auctionService.PlaceBidAsync(request.AuctionId, userId, request.Amount);
                return Ok(new { message = "Bid placed successfully" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class BidRequest
    {
        public string AuctionId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
