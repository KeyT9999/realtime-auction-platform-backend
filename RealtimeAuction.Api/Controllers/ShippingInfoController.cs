using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Shipping;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Helpers;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/shipping")]
[Authorize]
public class ShippingInfoController : ControllerBase
{
    private readonly IShippingInfoRepository _shippingInfoRepository;
    private readonly IAuctionRepository _auctionRepository;
    private readonly ILogger<ShippingInfoController> _logger;

    public ShippingInfoController(
        IShippingInfoRepository shippingInfoRepository,
        IAuctionRepository auctionRepository,
        ILogger<ShippingInfoController> logger)
    {
        _shippingInfoRepository = shippingInfoRepository;
        _auctionRepository = auctionRepository;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateShippingInfo([FromBody] CreateShippingInfoDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Verify auction exists and user owns it
            var auction = await _auctionRepository.GetByIdAsync(request.AuctionId);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";
            AuthorizationHelper.RequireOwnerOrAdmin(userId, auction.SellerId, userRole);

            // Check if shipping info already exists
            var existing = await _shippingInfoRepository.GetByAuctionIdAsync(request.AuctionId);
            if (existing != null)
            {
                return BadRequest(new { message = "Shipping info already exists for this auction" });
            }

            var shippingInfo = new ShippingInfo
            {
                AuctionId = request.AuctionId,
                Province = request.Province,
                FeeType = request.FeeType,
                ShippingFee = request.ShippingFee,
                Method = request.Method
            };

            var created = await _shippingInfoRepository.CreateAsync(shippingInfo);
            var response = new ShippingInfoResponseDto
            {
                Id = created.Id ?? "",
                AuctionId = created.AuctionId,
                Province = created.Province,
                FeeType = created.FeeType,
                ShippingFee = created.ShippingFee,
                Method = created.Method,
                CreatedAt = created.CreatedAt,
                UpdatedAt = created.UpdatedAt
            };

            return CreatedAtAction(nameof(GetShippingInfoByAuction), new { auctionId = request.AuctionId }, response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating shipping info");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("auction/{auctionId}")]
    public async Task<IActionResult> GetShippingInfoByAuction(string auctionId)
    {
        try
        {
            var shippingInfo = await _shippingInfoRepository.GetByAuctionIdAsync(auctionId);
            if (shippingInfo == null)
            {
                return NotFound(new { message = "Shipping info not found" });
            }

            var response = new ShippingInfoResponseDto
            {
                Id = shippingInfo.Id ?? "",
                AuctionId = shippingInfo.AuctionId,
                Province = shippingInfo.Province,
                FeeType = shippingInfo.FeeType,
                ShippingFee = shippingInfo.ShippingFee,
                Method = shippingInfo.Method,
                CreatedAt = shippingInfo.CreatedAt,
                UpdatedAt = shippingInfo.UpdatedAt
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting shipping info");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateShippingInfo(string id, [FromBody] UpdateShippingInfoDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var shippingInfo = await _shippingInfoRepository.GetByIdAsync(id);
            if (shippingInfo == null)
            {
                return NotFound(new { message = "Shipping info not found" });
            }

            // Verify user owns the auction
            var auction = await _auctionRepository.GetByIdAsync(shippingInfo.AuctionId);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";
            AuthorizationHelper.RequireOwnerOrAdmin(userId, auction.SellerId, userRole);

            if (!string.IsNullOrEmpty(request.Province))
                shippingInfo.Province = request.Province;
            if (request.FeeType.HasValue)
                shippingInfo.FeeType = request.FeeType.Value;
            if (request.ShippingFee.HasValue)
                shippingInfo.ShippingFee = request.ShippingFee;
            if (request.Method.HasValue)
                shippingInfo.Method = request.Method.Value;

            var updated = await _shippingInfoRepository.UpdateAsync(shippingInfo);
            var response = new ShippingInfoResponseDto
            {
                Id = updated.Id ?? "",
                AuctionId = updated.AuctionId,
                Province = updated.Province,
                FeeType = updated.FeeType,
                ShippingFee = updated.ShippingFee,
                Method = updated.Method,
                CreatedAt = updated.CreatedAt,
                UpdatedAt = updated.UpdatedAt
            };

            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating shipping info");
            return BadRequest(new { message = ex.Message });
        }
    }
}
