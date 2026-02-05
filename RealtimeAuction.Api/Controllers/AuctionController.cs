using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Auction;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/auctions")]
[Authorize]
public class AuctionController : ControllerBase
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IProductRepository _productRepository;
    private readonly IUserRepository _userRepository;
    private readonly IBidRepository _bidRepository;
    private readonly ILogger<AuctionController> _logger;

    public AuctionController(
        IAuctionRepository auctionRepository,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IUserRepository userRepository,
        IBidRepository bidRepository,
        ILogger<AuctionController> logger)
    {
        _auctionRepository = auctionRepository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
        _bidRepository = bidRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuctions(
        [FromQuery] AuctionStatus? status = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] string? sellerId = null)
    {
        try
        {
            List<Auction> auctions;
            if (status.HasValue)
            {
                auctions = await _auctionRepository.GetByStatusAsync(status.Value);
            }
            else if (!string.IsNullOrEmpty(categoryId))
            {
                auctions = await _auctionRepository.GetByCategoryIdAsync(categoryId);
            }
            else if (!string.IsNullOrEmpty(sellerId))
            {
                auctions = await _auctionRepository.GetBySellerIdAsync(sellerId);
            }
            else
            {
                auctions = await _auctionRepository.GetAllAsync();
            }

            var response = await MapToResponseDtos(auctions);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auctions");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetAuctionById(string id)
    {
        try
        {
            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            var response = await MapToResponseDto(auction);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auction by ID");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateAuction([FromBody] CreateAuctionDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            // Validate category exists
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
            if (category == null)
            {
                return BadRequest(new { message = "Category not found" });
            }

            // Validate product exists
            var product = await _productRepository.GetByIdAsync(request.ProductId);
            if (product == null)
            {
                return BadRequest(new { message = "Product not found" });
            }

            // Validate images count (1-5)
            if (request.Images == null || request.Images.Count < 1 || request.Images.Count > 5)
            {
                return BadRequest(new { message = "Auction must have between 1 and 5 images" });
            }

            // Validate starting price minimum
            if (request.StartingPrice < 1000)
            {
                return BadRequest(new { message = "Starting price must be at least 1,000 VND" });
            }

            // Validate bid increment minimum
            if (request.BidIncrement < 1000)
            {
                return BadRequest(new { message = "Bid increment must be at least 1,000 VND" });
            }

            // Validate duration (minimum 60 minutes)
            if (request.Duration < 60)
            {
                return BadRequest(new { message = "Auction duration must be at least 60 minutes (1 hour)" });
            }

            // Validate EndTime > StartTime
            if (request.EndTime <= request.StartTime)
            {
                return BadRequest(new { message = "End time must be after start time" });
            }

            var auction = new Auction
            {
                Title = request.Title,
                Description = request.Description,
                StartingPrice = request.StartingPrice,
                CurrentPrice = request.StartingPrice,
                ReservePrice = request.ReservePrice,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Duration = request.Duration,
                Status = AuctionStatus.Draft,
                SellerId = userId,
                CategoryId = request.CategoryId,
                ProductId = request.ProductId,
                Images = request.Images,
                BidIncrement = request.BidIncrement,
                AutoExtendDuration = request.AutoExtendDuration
            };

            var created = await _auctionRepository.CreateAsync(auction);
            var response = await MapToResponseDto(created);
            return CreatedAtAction(nameof(GetAuctionById), new { id = response.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating auction");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAuction(string id, [FromBody] UpdateAuctionDto request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            // Check authorization: User can only update their own auctions, Admin can update any
            AuthorizationHelper.RequireOwnerOrAdmin(userId, auction.SellerId, userRole);

            if (!string.IsNullOrEmpty(request.Title))
                auction.Title = request.Title;
            if (request.Description != null)
                auction.Description = request.Description;
            if (request.StartingPrice.HasValue)
            {
                auction.StartingPrice = request.StartingPrice.Value;
                // Only update current price if no bids have been placed
                var bids = await _bidRepository.GetByAuctionIdAsync(id);
                if (!bids.Any())
                {
                    auction.CurrentPrice = request.StartingPrice.Value;
                }
            }
            if (request.ReservePrice.HasValue)
                auction.ReservePrice = request.ReservePrice;
            if (request.StartTime.HasValue)
                auction.StartTime = request.StartTime.Value;
            if (request.EndTime.HasValue)
                auction.EndTime = request.EndTime.Value;
            if (request.Duration.HasValue)
                auction.Duration = request.Duration.Value;
            if (!string.IsNullOrEmpty(request.CategoryId))
            {
                var category = await _categoryRepository.GetByIdAsync(request.CategoryId);
                if (category == null)
                {
                    return BadRequest(new { message = "Category not found" });
                }
                auction.CategoryId = request.CategoryId;
            }
            if (request.Images != null)
                auction.Images = request.Images;
            if (request.BidIncrement.HasValue)
                auction.BidIncrement = request.BidIncrement.Value;
            if (request.AutoExtendDuration.HasValue)
                auction.AutoExtendDuration = request.AutoExtendDuration;

            var updated = await _auctionRepository.UpdateAsync(auction);
            var response = await MapToResponseDto(updated);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auction");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAuction(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "User";

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            // Check authorization: User can only delete their own auctions, Admin can delete any
            AuthorizationHelper.RequireOwnerOrAdmin(userId, auction.SellerId, userRole);

            var result = await _auctionRepository.DeleteAsync(id);
            if (result)
            {
                return Ok(new { message = "Auction deleted successfully" });
            }
            return NotFound(new { message = "Auction not found" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting auction");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}/status")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> UpdateAuctionStatus(string id, [FromBody] UpdateAuctionStatusDto request)
    {
        try
        {
            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            // Validate status transition
            var isValidTransition = await _auctionRepository.ValidateStatusTransitionAsync(auction.Status, request.Status);
            if (!isValidTransition)
            {
                return BadRequest(new { message = $"Invalid status transition from {auction.Status} to {request.Status}" });
            }

            auction.Status = request.Status;
            var updated = await _auctionRepository.UpdateAsync(auction);
            var response = await MapToResponseDto(updated);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating auction status");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("stats")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> GetAuctionStats()
    {
        try
        {
            var allAuctions = await _auctionRepository.GetAllAsync();
            var completedAuctions = allAuctions.Where(a => a.Status == AuctionStatus.Completed).ToList();

            var stats = new AuctionStatsDto
            {
                TotalAuctions = allAuctions.Count,
                ActiveAuctions = allAuctions.Count(a => a.Status == AuctionStatus.Active),
                DraftAuctions = allAuctions.Count(a => a.Status == AuctionStatus.Draft),
                CompletedAuctions = completedAuctions.Count,
                CancelledAuctions = allAuctions.Count(a => a.Status == AuctionStatus.Cancelled),
                PendingAuctions = allAuctions.Count(a => a.Status == AuctionStatus.Pending),
                TotalRevenue = completedAuctions.Any() ? completedAuctions.Sum(a => a.CurrentPrice) : null,
                AveragePrice = completedAuctions.Any() ? completedAuctions.Average(a => a.CurrentPrice) : null
            };
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auction stats");
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<AuctionResponseDto> MapToResponseDto(Auction auction)
    {
        var category = await _categoryRepository.GetByIdAsync(auction.CategoryId);
        var product = await _productRepository.GetByIdAsync(auction.ProductId);
        var seller = await _userRepository.GetByIdAsync(auction.SellerId);
        var bids = await _bidRepository.GetByAuctionIdAsync(auction.Id ?? "");

        return new AuctionResponseDto
        {
            Id = auction.Id ?? "",
            Title = auction.Title,
            Description = auction.Description,
            StartingPrice = auction.StartingPrice,
            CurrentPrice = auction.CurrentPrice,
            ReservePrice = auction.ReservePrice,
            StartTime = auction.StartTime,
            EndTime = auction.EndTime,
            Duration = auction.Duration,
            Status = auction.Status,
            SellerId = auction.SellerId,
            SellerName = seller?.FullName,
            CategoryId = auction.CategoryId,
            CategoryName = category?.Name,
            ProductId = auction.ProductId,
            Product = product != null ? new Dtos.Product.ProductResponseDto
            {
                Id = product.Id ?? "",
                Name = product.Name,
                Description = product.Description,
                Condition = product.Condition,
                Category = product.Category,
                Brand = product.Brand,
                Model = product.Model,
                Year = product.Year,
                Images = product.Images,
                Specifications = product.Specifications
            } : null,
            Images = auction.Images,
            BidIncrement = auction.BidIncrement,
            AutoExtendDuration = auction.AutoExtendDuration,
            CreatedAt = auction.CreatedAt,
            UpdatedAt = auction.UpdatedAt,
            BidCount = bids.Count
        };
    }

    private async Task<List<AuctionResponseDto>> MapToResponseDtos(List<Auction> auctions)
    {
        var result = new List<AuctionResponseDto>();
        foreach (var auction in auctions)
        {
            result.Add(await MapToResponseDto(auction));
        }
        return result;
    }
}
