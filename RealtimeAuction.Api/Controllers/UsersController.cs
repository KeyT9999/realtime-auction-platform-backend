using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.User;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Services;
using System.Security.Claims;
using MongoDB.Driver;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMongoCollection<User> _users;
    private readonly IMongoCollection<Auction> _auctions;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IMongoDatabase database, ILogger<UsersController> logger)
    {
        _users = database.GetCollection<User>("Users");
        _auctions = database.GetCollection<Auction>("Auctions");
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            var response = new UserProfileResponse
            {
                Id = user.Id!,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role ?? "User",
                IsEmailVerified = user.IsEmailVerified,
                Phone = user.Phone,
                Address = user.Address,
                AvailableBalance = user.AvailableBalance,
                EscrowBalance = user.EscrowBalance,
                CreatedAt = user.CreatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user profile");
            return BadRequest(new { message = "An error occurred while retrieving profile" });
        }
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Update user profile
            user.FullName = request.FullName;
            user.Phone = request.Phone;
            user.Address = request.Address;
            user.UpdatedAt = DateTime.UtcNow;

            await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

            var response = new UserProfileResponse
            {
                Id = user.Id!,
                Email = user.Email,
                FullName = user.FullName,
                Role = user.Role ?? "User",
                IsEmailVerified = user.IsEmailVerified,
                Phone = user.Phone,
                Address = user.Address,
                AvailableBalance = user.AvailableBalance,
                EscrowBalance = user.EscrowBalance,
                CreatedAt = user.CreatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user profile");
            return BadRequest(new { message = "An error occurred while updating profile" });
        }
    }

    [HttpGet("{id}/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSellerStats(string id)
    {
        try
        {
            var user = await _users.Find(u => u.Id == id).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            // Get all auctions for this seller
            var sellerAuctions = await _auctions.Find(a => a.SellerId == id).ToListAsync();

            var totalAuctions = sellerAuctions.Count;
            var completedAuctions = sellerAuctions.Count(a => a.Status == AuctionStatus.Completed);
            var activeAuctions = sellerAuctions.Count(a => a.Status == AuctionStatus.Active);

            var completionRate = totalAuctions > 0 
                ? (decimal)completedAuctions / totalAuctions * 100 
                : 0;

            var response = new SellerStatsResponse
            {
                TotalAuctions = totalAuctions,
                CompletedAuctions = completedAuctions,
                ActiveAuctions = activeAuctions,
                CompletionRate = Math.Round(completionRate, 2),
                JoinedDate = user.CreatedAt,
                AverageRating = null // For future implementation
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting seller stats");
            return BadRequest(new { message = "An error occurred while retrieving seller stats" });
        }
    }
}
