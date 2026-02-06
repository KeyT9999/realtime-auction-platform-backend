using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using RealtimeAuction.Api.Dtos.Auction;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Hubs;
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
    private readonly IHubContext<AuctionHub> _hubContext;
    private readonly ILogger<AuctionController> _logger;

    public AuctionController(
        IAuctionRepository auctionRepository,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IUserRepository userRepository,
        IBidRepository bidRepository,
        IHubContext<AuctionHub> hubContext,
        ILogger<AuctionController> logger)
    {
        _auctionRepository = auctionRepository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
        _bidRepository = bidRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous] // Allow viewing auction list without login
    public async Task<IActionResult> GetAuctions(
        [FromQuery] string? keyword = null,
        [FromQuery] AuctionStatus? status = null,
        [FromQuery] string? categoryId = null,
        [FromQuery] string? sellerId = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? timeFilter = null,
        [FromQuery] string sortBy = "startTime",
        [FromQuery] string sortOrder = "desc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        try
        {
            // Use advanced search if any filter is provided
            if (!string.IsNullOrEmpty(keyword) || 
                minPrice.HasValue || 
                maxPrice.HasValue || 
                !string.IsNullOrEmpty(timeFilter))
            {
                var (items, searchTotalCount) = await _auctionRepository.SearchAuctionsAsync(
                    keyword, status, categoryId, minPrice, maxPrice, 
                    timeFilter, sortBy, sortOrder, page, pageSize);

                var response = await MapToResponseDtos(items);
                var searchTotalPages = (int)Math.Ceiling((double)searchTotalCount / pageSize);

                return Ok(new
                {
                    items = response,
                    totalCount = searchTotalCount,
                    page,
                    pageSize,
                    totalPages = searchTotalPages
                });
            }

            // Fallback to simple queries for backward compatibility
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

            // Apply pagination to simple queries
            var simpleTotalCount = auctions.Count;
            var paginatedAuctions = auctions
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var simpleResponse = await MapToResponseDtos(paginatedAuctions);
            var simpleTotalPages = (int)Math.Ceiling((double)simpleTotalCount / pageSize);

            return Ok(new
            {
                items = simpleResponse,
                totalCount = simpleTotalCount,
                page,
                pageSize,
                totalPages = simpleTotalPages
            });
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

            // Auto-activate auction if it's time to start
            if (auction.Status == AuctionStatus.Draft)
            {
                var now = DateTime.UtcNow;
                if (now >= auction.StartTime && now < auction.EndTime)
                {
                    _logger.LogInformation($"Auto-activating auction {id} - time has come");
                    auction.Status = AuctionStatus.Active;
                    await _auctionRepository.UpdateAsync(auction);
                }
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

            // Validate buyout price if provided
            if (request.BuyoutPrice.HasValue)
            {
                if (request.BuyoutPrice.Value < request.StartingPrice * 1.5m)
                {
                    return BadRequest(new { message = "Buyout price must be at least 1.5x the starting price" });
                }
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
                AutoExtendDuration = request.AutoExtendDuration,
                BuyoutPrice = request.BuyoutPrice
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
            if (request.BuyoutPrice.HasValue)
            {
                // Validate buyout price
                if (request.BuyoutPrice.Value < auction.StartingPrice * 1.5m)
                {
                    return BadRequest(new { message = "Buyout price must be at least 1.5x the starting price" });
                }
                auction.BuyoutPrice = request.BuyoutPrice;
            }

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
        
        // Get winner info if exists
        User? winner = null;
        if (!string.IsNullOrEmpty(auction.WinnerId))
        {
            winner = await _userRepository.GetByIdAsync(auction.WinnerId);
        }

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
            BuyoutPrice = auction.BuyoutPrice,
            WinnerId = auction.WinnerId,
            WinnerName = winner?.FullName,
            EndReason = auction.EndReason,
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

    [HttpPost("{id}/accept-bid")]
    public async Task<IActionResult> AcceptBid(string id, [FromBody] AcceptBidDto? request = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            // Only seller can accept bid
            if (auction.SellerId != userId)
            {
                return Forbid();
            }

            // Must be Active status
            if (auction.Status != AuctionStatus.Active)
            {
                return BadRequest(new { message = "Can only accept bids for active auctions" });
            }

            // Must have at least 1 bid
            var bids = await _bidRepository.GetByAuctionIdAsync(id);
            if (!bids.Any())
            {
                return BadRequest(new { message = "No bids to accept" });
            }

            // Current price must be >= reserve price (if set)
            if (auction.ReservePrice.HasValue && auction.CurrentPrice < auction.ReservePrice.Value)
            {
                return BadRequest(new { message = $"Current price ({auction.CurrentPrice:N0}) must reach reserve price ({auction.ReservePrice:N0}) to accept" });
            }

            // Get highest bidder
            var highestBid = bids.OrderByDescending(b => b.Amount).First();

            // Update auction
            auction.Status = AuctionStatus.Completed;
            auction.WinnerId = highestBid.UserId;
            auction.EndReason = "accepted";
            auction.UpdatedAt = DateTime.UtcNow;
            
            await _auctionRepository.UpdateAsync(auction);

            // Get winner info
            var winner = await _userRepository.GetByIdAsync(highestBid.UserId);

            // Notify via SignalR
            await AuctionHub.NotifyAuctionAccepted(_hubContext, id, new
            {
                AuctionId = id,
                AuctionTitle = auction.Title,
                WinnerId = highestBid.UserId,
                WinnerName = winner?.FullName,
                WinningBid = highestBid.Amount,
                Message = request?.Message
            });

            var response = await MapToResponseDto(auction);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting bid");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/buyout")]
    public async Task<IActionResult> Buyout(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            // Cannot buyout own auction
            if (auction.SellerId == userId)
            {
                return BadRequest(new { message = "You cannot buyout your own auction" });
            }

            // Must be Active
            if (auction.Status != AuctionStatus.Active)
            {
                return BadRequest(new { message = "Auction is not active" });
            }

            // Must have buyout price set
            if (!auction.BuyoutPrice.HasValue)
            {
                return BadRequest(new { message = "This auction does not have a buyout option" });
            }

            // Create final bid with buyout price
            var buyoutBid = new Bid
            {
                AuctionId = id,
                UserId = userId,
                Amount = auction.BuyoutPrice.Value,
                Timestamp = DateTime.UtcNow
            };
            
            await _bidRepository.CreateAsync(buyoutBid, auction.CurrentPrice, auction.BidIncrement);
            await _auctionRepository.UpdateCurrentPriceAsync(id, auction.BuyoutPrice.Value);

            // Complete auction immediately
            auction.Status = AuctionStatus.Completed;
            auction.CurrentPrice = auction.BuyoutPrice.Value;
            auction.WinnerId = userId;
            auction.EndReason = "buyout";
            auction.UpdatedAt = DateTime.UtcNow;
            
            await _auctionRepository.UpdateAsync(auction);

            // Get buyer info
            var buyer = await _userRepository.GetByIdAsync(userId);

            // Notify via SignalR
            await AuctionHub.NotifyAuctionBuyout(_hubContext, id, new
            {
                AuctionId = id,
                AuctionTitle = auction.Title,
                BuyerId = userId,
                BuyerName = buyer?.FullName,
                BuyoutPrice = auction.BuyoutPrice.Value
            });

            var response = await MapToResponseDto(auction);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing buyout");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> CancelAuction(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            // Only seller can cancel
            if (auction.SellerId != userId)
            {
                return Forbid();
            }

            // Check if can cancel
            var bids = await _bidRepository.GetByAuctionIdAsync(id);
            if (auction.Status == AuctionStatus.Active && bids.Any())
            {
                return BadRequest(new { message = "Cannot cancel auction with existing bids. Consider accepting the current bid instead." });
            }

            // Can only cancel Draft or Active (with no bids)
            if (auction.Status != AuctionStatus.Draft && auction.Status != AuctionStatus.Active)
            {
                return BadRequest(new { message = "Can only cancel draft or active auctions" });
            }

            // Update auction
            auction.Status = AuctionStatus.Cancelled;
            auction.EndReason = "cancelled";
            auction.UpdatedAt = DateTime.UtcNow;
            
            await _auctionRepository.UpdateAsync(auction);

            // Notify via SignalR if had bidders/watchers
            await AuctionHub.NotifyAuctionCancelled(_hubContext, id, new
            {
                AuctionId = id,
                AuctionTitle = auction.Title,
                Reason = "Seller cancelled the auction"
            });

            var response = await MapToResponseDto(auction);
            return Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling auction");
            return BadRequest(new { message = ex.Message });
        }
    }
}
