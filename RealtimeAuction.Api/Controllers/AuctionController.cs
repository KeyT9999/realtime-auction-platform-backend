using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Bson;
using MongoDB.Driver;
using RealtimeAuction.Api.Dtos.Auction;
using RealtimeAuction.Api.Dtos.Bid;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Hubs;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
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
    private readonly IBidService _bidService;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IOrderService _orderService;
    private readonly IEmailService _emailService;
    private readonly IHubContext<AuctionHub> _hubContext;
    private readonly INotificationRepository _notificationRepository;
    private readonly ILogger<AuctionController> _logger;
    private readonly IConfiguration _configuration;

    public AuctionController(
        IAuctionRepository auctionRepository,
        ICategoryRepository categoryRepository,
        IProductRepository productRepository,
        IUserRepository userRepository,
        IBidRepository bidRepository,
        IBidService bidService,
        ITransactionRepository transactionRepository,
        IOrderService orderService,
        IEmailService emailService,
        IHubContext<AuctionHub> hubContext,
        INotificationRepository notificationRepository,
        ILogger<AuctionController> logger,
        IConfiguration configuration)
    {
        _auctionRepository = auctionRepository;
        _categoryRepository = categoryRepository;
        _productRepository = productRepository;
        _userRepository = userRepository;
        _bidRepository = bidRepository;
        _bidService = bidService;
        _transactionRepository = transactionRepository;
        _orderService = orderService;
        _emailService = emailService;
        _hubContext = hubContext;
        _notificationRepository = notificationRepository;
        _logger = logger;
        _configuration = configuration;
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
            List<string>? productIds = null;
            List<string>? additionalCategoryIds = null;

            if (!string.IsNullOrEmpty(keyword))
            {
                // 1. Search matching products
                productIds = await _productRepository.SearchProductIdsAsync(keyword);

                // 2. Search matching categories (keyword extension)
                var keywords = RealtimeAuction.Api.Helpers.SearchHelper.GetExpandedKeywords(keyword);
                var allCategories = await _categoryRepository.GetAllAsync();
                additionalCategoryIds = allCategories
                    .Where(c => keywords.Any(k => 
                        (c.Name != null && c.Name.Contains(k, StringComparison.OrdinalIgnoreCase)) ||
                        (c.Description != null && c.Description.Contains(k, StringComparison.OrdinalIgnoreCase))))
                    .Select(c => c.Id!)
                    .ToList();
            }

            // If we found relevant categories from the keyword, we want to include them in the search
            // We pass categoryId (if any) and additionalCategoryIds to the repository
            var (items, totalCount) = await _auctionRepository.SearchAuctionsAsync(
                keyword, productIds, status, categoryId, sellerId, minPrice, maxPrice,
                timeFilter, sortBy, sortOrder, page, pageSize, additionalCategoryIds);

            var response = await MapToResponseDtos(items);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return Ok(new
            {
                items = response,
                totalCount,
                page,
                pageSize,
                totalPages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auctions");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    [AllowAnonymous] // Allow viewing auction detail without login
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

    [HttpGet("{id}/similar")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSimilarAuctions(string id, [FromQuery] int limit = 8)
    {
        try
        {
            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
            {
                return NotFound(new { message = "Auction not found" });
            }

            if (limit < 1) limit = 4;
            if (limit > 20) limit = 20;

            var similarAuctions = await _auctionRepository.GetSimilarAuctionsAsync(
                id,
                auction.CategoryId,
                auction.CurrentPrice,
                limit
            );

            var results = new List<object>();
            foreach (var a in similarAuctions)
            {
                var dto = await MapToResponseDto(a);
                results.Add(dto);
            }

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting similar auctions");
            return BadRequest(new { message = ex.Message });
        }
    }

    // ══════════ APPROVAL WORKFLOW ══════════

    [HttpPost("{id}/submit-for-approval")]
    public async Task<IActionResult> SubmitForApproval(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { message = "User not authenticated" });

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
                return NotFound(new { message = "Auction not found" });
            if (auction.SellerId != userId)
                return Forbid();
            if (auction.Status != AuctionStatus.Draft && auction.Status != AuctionStatus.Rejected)
                return BadRequest(new { message = "Chỉ có thể gửi duyệt từ trạng thái Nháp hoặc Bị từ chối" });

            auction.Status = AuctionStatus.PendingApproval;
            auction.SubmittedAt = DateTime.UtcNow;
            auction.RejectionReason = null; // clear previous rejection
            await _auctionRepository.UpdateAsync(auction);

            _logger.LogInformation("Auction {Id} submitted for approval by {UserId}", id, userId);
            return Ok(new { message = "Đã gửi yêu cầu duyệt thành công" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting auction for approval");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveAuction(string id)
    {
        try
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized(new { message = "Admin not authenticated" });

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
                return NotFound(new { message = "Auction not found" });
            if (auction.Status != AuctionStatus.PendingApproval)
                return BadRequest(new { message = "Chỉ có thể duyệt auction đang chờ duyệt" });

            var now = DateTime.UtcNow;
            auction.Status = auction.StartTime <= now ? AuctionStatus.Active : AuctionStatus.Pending;
            auction.ApprovedBy = adminId;
            auction.ApprovedAt = now;
            await _auctionRepository.UpdateAsync(auction);

            // Notify seller via email
            try
            {
                var seller = await _userRepository.GetByIdAsync(auction.SellerId);
                if (seller != null && !string.IsNullOrEmpty(seller.Email))
                {
                    await _emailService.SendAuctionApprovedEmailAsync(
                        seller.Email,
                        seller.FullName,
                        auction.Title,
                        auction.Id!
                    );
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send auction approved email");
            }

            // In-app notification
            try
            {
                var notification = new Notification
                {
                    UserId = auction.SellerId,
                    Title = "Đấu giá đã được duyệt",
                    Message = $"Phiên đấu giá \"{auction.Title}\" đã được admin phê duyệt và đang hoạt động!",
                    Type = "AuctionApproved",
                    RelatedId = auction.Id,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _notificationRepository.CreateAsync(notification);
            }
            catch (Exception notifEx)
            {
                _logger.LogError(notifEx, "Failed to create approval notification");
            }

            _logger.LogInformation("Auction {Id} approved by admin {AdminId}", id, adminId);
            return Ok(new { message = "Đã duyệt phiên đấu giá thành công" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving auction");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectAuction(string id, [FromBody] RejectAuctionRequest request)
    {
        try
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized(new { message = "Admin not authenticated" });

            if (string.IsNullOrWhiteSpace(request?.Reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối" });

            var auction = await _auctionRepository.GetByIdAsync(id);
            if (auction == null)
                return NotFound(new { message = "Auction not found" });
            if (auction.Status != AuctionStatus.PendingApproval)
                return BadRequest(new { message = "Chỉ có thể từ chối auction đang chờ duyệt" });

            auction.Status = AuctionStatus.Rejected;
            auction.RejectionReason = request.Reason;
            auction.ApprovedBy = null;
            auction.ApprovedAt = null;
            await _auctionRepository.UpdateAsync(auction);

            // Notify seller via email
            try
            {
                var seller = await _userRepository.GetByIdAsync(auction.SellerId);
                if (seller != null && !string.IsNullOrEmpty(seller.Email))
                {
                    await _emailService.SendAuctionRejectedEmailAsync(
                        seller.Email,
                        seller.FullName,
                        auction.Title,
                        request.Reason
                    );
                }
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send auction rejected email");
            }

            // In-app notification
            try
            {
                var notification = new Notification
                {
                    UserId = auction.SellerId,
                    Title = "Đấu giá bị từ chối",
                    Message = $"Phiên đấu giá \"{auction.Title}\" đã bị từ chối. Lý do: {request.Reason}",
                    Type = "AuctionRejected",
                    RelatedId = auction.Id,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };
                await _notificationRepository.CreateAsync(notification);
            }
            catch (Exception notifEx)
            {
                _logger.LogError(notifEx, "Failed to create rejection notification");
            }

            _logger.LogInformation("Auction {Id} rejected by admin {AdminId}. Reason: {Reason}", id, adminId, request.Reason);
            return Ok(new { message = "Đã từ chối phiên đấu giá" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting auction");
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

            // Normalize times to UTC to avoid timezone mismatches across clients/servers.
            // Frontend usually sends ISO with 'Z' (UTC), but we guard for Unspecified/Local values.
            var startUtc = request.StartTime.Kind == DateTimeKind.Utc ? request.StartTime : request.StartTime.ToUniversalTime();
            var endUtc = request.EndTime.Kind == DateTimeKind.Utc ? request.EndTime : request.EndTime.ToUniversalTime();

            // Determine initial status
            // Always set to Draft so that submitForApproval can process it.
            var initialStatus = AuctionStatus.Draft;

            var auction = new Auction
            {
                Title = request.Title,
                Description = request.Description,
                StartingPrice = request.StartingPrice,
                CurrentPrice = request.StartingPrice,
                ReservePrice = request.ReservePrice,
                StartTime = startUtc,
                EndTime = endUtc,
                Duration = request.Duration,
                Status = initialStatus,
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

            // Realtime: notify admins and seller about new auction creation
            await _hubContext.Clients.Group(GroupNames.Admins).SendAsync("AdminNotification", new
            {
                type = "AuctionCreated",
                auctionId = response.Id,
                sellerId = userId,
                message = $"[Auction] Tạo đấu giá mới: {response.Title}"
            });

            await _hubContext.Clients.Group(GroupNames.User(userId)).SendAsync("UserNotification", new
            {
                type = "AuctionCreated",
                auctionId = response.Id,
                message = $"Bạn đã tạo đấu giá: {response.Title}"
            });
            await _notificationRepository.CreateAsync(new Notification
            {
                UserId = userId,
                Type = "AuctionCreated",
                Title = "Đấu giá đã tạo",
                Message = $"Bạn đã tạo đấu giá: {response.Title}",
                RelatedId = response.Id
            });

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
            {
                var v = request.StartTime.Value;
                auction.StartTime = v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime();
            }
            if (request.EndTime.HasValue)
            {
                var v = request.EndTime.Value;
                auction.EndTime = v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime();
            }
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

            // Realtime: auction status changed
            await _hubContext.Clients.Group(GroupNames.Auction(id)).SendAsync("AuctionStatusChanged", new
            {
                auctionId = id,
                status = (int)response.Status,
                message = $"Trạng thái đấu giá thay đổi: {response.Status}"
            });

            // Notify seller + admins
            await _hubContext.Clients.Group(GroupNames.User(response.SellerId)).SendAsync("UserNotification", new
            {
                type = "AuctionStatusChanged",
                auctionId = id,
                status = (int)response.Status,
                message = $"Trạng thái đấu giá \"{response.Title}\" -> {response.Status}"
            });
            await _notificationRepository.CreateAsync(new Notification
            {
                UserId = response.SellerId,
                Type = "AuctionStatusChanged",
                Title = "Trạng thái đấu giá",
                Message = $"Trạng thái đấu giá \"{response.Title}\" -> {response.Status}",
                RelatedId = id
            });

            await _hubContext.Clients.Group(GroupNames.Admins).SendAsync("AdminNotification", new
            {
                type = "AuctionStatusChanged",
                auctionId = id,
                status = (int)response.Status,
                message = $"[Auction] Status {response.Status} for {response.Title}"
            });

            if (response.Status == AuctionStatus.Completed || response.Status == AuctionStatus.Cancelled)
            {
                await _hubContext.Clients.Group(GroupNames.Auction(id)).SendAsync("AuctionEnded", new
                {
                    auctionId = id,
                    status = (int)response.Status
                });
            }

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
                DraftAuctions = 0, // Draft status removed - always 0
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
            BidCount = bids.Count,
            RejectionReason = auction.RejectionReason,
            ApprovedAt = auction.ApprovedAt,
            SubmittedAt = auction.SubmittedAt
        };
    }

    private async Task<List<AuctionResponseDto>> MapToResponseDtos(List<Auction> auctions)
    {
        if (auctions.Count == 0) return new List<AuctionResponseDto>();

        var categoryIds = auctions.Select(a => a.CategoryId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var productIds = auctions.Select(a => a.ProductId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var sellerIds = auctions.Select(a => a.SellerId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var winnerIds = auctions.Select(a => a.WinnerId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var auctionIds = auctions.Select(a => a.Id ?? "").Where(id => !string.IsNullOrEmpty(id)).ToList();
        var allUserIds = sellerIds.Union(winnerIds).Distinct().ToList();

        var categoriesTask = Task.WhenAll(categoryIds.Select(id => _categoryRepository.GetByIdAsync(id)));
        var productsTask = Task.WhenAll(productIds.Select(id => _productRepository.GetByIdAsync(id)));
        var usersTask = Task.WhenAll(allUserIds.Select(id => _userRepository.GetByIdAsync(id)));
        var bidsTask = Task.WhenAll(auctionIds.Select(id => _bidRepository.GetByAuctionIdAsync(id)));

        await Task.WhenAll(categoriesTask, productsTask, usersTask, bidsTask);

        var categoryMap = (await categoriesTask).Where(c => c != null).ToDictionary(c => c!.Id ?? "", c => c!);
        var productMap = (await productsTask).Where(p => p != null).ToDictionary(p => p!.Id ?? "", p => p!);
        var userMap = (await usersTask).Where(u => u != null).ToDictionary(u => u!.Id ?? "", u => u!);
        var bidsMap = auctionIds.Zip(await bidsTask, (id, bids) => (id, bids)).ToDictionary(x => x.id, x => x.bids);

        return auctions.Select(auction =>
        {
            categoryMap.TryGetValue(auction.CategoryId ?? "", out var category);
            productMap.TryGetValue(auction.ProductId ?? "", out var product);
            userMap.TryGetValue(auction.SellerId ?? "", out var seller);
            userMap.TryGetValue(auction.WinnerId ?? "", out var winner);
            bidsMap.TryGetValue(auction.Id ?? "", out var bids);

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
                BidCount = bids?.Count ?? 0,
                RejectionReason = auction.RejectionReason,
                ApprovedAt = auction.ApprovedAt,
                SubmittedAt = auction.SubmittedAt
            };
        }).ToList();

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
            // Get highest bidder
            var highestBid = bids.OrderByDescending(b => b.Amount).First();

            // Enforce reserve price against the actual highest bid amount (more reliable than CurrentPrice if it ever gets out of sync)
            if (auction.ReservePrice.HasValue && highestBid.Amount < auction.ReservePrice.Value)
            {
                return BadRequest(new
                {
                    message = $"Giá hiện tại ({highestBid.Amount:N0}) chưa đạt reserve price ({auction.ReservePrice.Value:N0})"
                });
            }

            // Kiểm tra winner có hold balance đủ không
            var winner = await _userRepository.GetByIdAsync(highestBid.UserId);
            if (winner == null)
            {
                return BadRequest(new { message = "Winner not found" });
            }

            // Kiểm tra hold balance
            var userBids = bids.Where(b => b.UserId == highestBid.UserId && !b.IsHoldReleased).ToList();
            var totalHeld = userBids.Any() ? userBids.Max(b => b.HeldAmount) : 0;

            if (totalHeld < highestBid.Amount)
            {
                // Cần hold thêm
                var neededAmount = highestBid.Amount - totalHeld;
                if (winner.AvailableBalance < neededAmount)
                {
                    return BadRequest(new { message = $"Winner không đủ số dư. Cần {highestBid.Amount:N0}đ, hiện có hold {totalHeld:N0}đ và available {winner.AvailableBalance:N0}đ" });
                }

                // Đảm bảo hold đủ
                var ensured = await _bidService.EnsureHoldAsync(highestBid.UserId, id, highestBid.Amount);
                if (!ensured)
                {
                    return BadRequest(new { message = "Không thể đảm bảo hold balance cho winner" });
                }
            }

            // Release hold của các losers
            await _bidService.ReleaseAllHoldsExceptWinnerAsync(id, highestBid.UserId);

            // Update auction
            auction.Status = AuctionStatus.Completed;
            auction.WinnerId = highestBid.UserId;
            auction.EndReason = "accepted";
            auction.UpdatedAt = DateTime.UtcNow;
            
            await _auctionRepository.UpdateAsync(auction);

            // Tạo Transaction Pending (giữ hold, chờ confirm)
            var updatedWinner = await _userRepository.GetByIdAsync(highestBid.UserId);
            if (updatedWinner != null)
            {
                var activeBid = bids
                    .Where(b => b.UserId == highestBid.UserId && !b.IsHoldReleased)
                    .OrderByDescending(b => b.Amount)
                    .FirstOrDefault();

                if (activeBid != null)
                {
                    var pendingTransaction = new Transaction
                    {
                        UserId = highestBid.UserId,
                        Type = TransactionType.Payment,
                        Amount = -activeBid.HeldAmount,
                        Description = $"Thanh toán đấu giá (seller chấp nhận) - chờ xác nhận",
                        RelatedAuctionId = id,
                        RelatedBidId = activeBid.Id,
                        Status = TransactionStatus.Pending,
                        BuyerConfirmed = false,
                        SellerConfirmed = false,
                        BalanceBefore = updatedWinner.EscrowBalance,
                        BalanceAfter = updatedWinner.EscrowBalance, // Giữ nguyên vì chưa chuyển
                        CreatedAt = DateTime.UtcNow
                    };
                    await _transactionRepository.CreateAsync(pendingTransaction);
                }
            }

            // Tạo Order cho giao dịch (để hiển thị trong My Orders / My Sales)
            try
            {
                var productImage = auction.Images?.FirstOrDefault();
                await _orderService.CreateOrderFromAuctionAsync(
                    id,
                    highestBid.UserId,
                    auction.SellerId,
                    highestBid.Amount,
                    auction.Title,
                    productImage
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create order for accepted auction {AuctionId}", id);
            }

            // Get winner info (reuse existing winner variable)
            winner = await _userRepository.GetByIdAsync(highestBid.UserId);

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

            // Send Bid Accepted email to winner
            if (winner != null && !string.IsNullOrEmpty(winner.Email))
            {
                try
                {
                    var seller = await _userRepository.GetByIdAsync(userId);
                    var transactionUrl = $"{_configuration["FrontendUrl"]}/auctions/{auction.Id}";
                    
                    await _emailService.SendBidAcceptedEmailAsync(
                        winner.Email,
                        winner.FullName,
                        auction.Title,
                        FormatCurrency(highestBid.Amount),
                        seller?.FullName ?? "Người bán",
                        transactionUrl);
                    
                    _logger.LogInformation("Sent bid accepted email to {Email}", winner.Email);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send bid accepted email to {Email}", winner.Email);
                }
            }

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

            // Sử dụng BidService để hold balance
            var buyoutBidDto = new CreateBidDto
            {
                AuctionId = id,
                Amount = auction.BuyoutPrice.Value
            };

            var bidResult = await _bidService.PlaceBidAsync(userId, buyoutBidDto);
            if (!bidResult.Success)
            {
                return BadRequest(new { message = bidResult.ErrorMessage });
            }

            // Release hold của các bidders khác (nếu có)
            var allBids = await _bidRepository.GetByAuctionIdAsync(id);
            var otherBidders = allBids
                .Where(b => b.UserId != userId && !b.IsHoldReleased)
                .Select(b => b.UserId)
                .Distinct()
                .ToList();

            foreach (var otherUserId in otherBidders)
            {
                await _bidService.ReleaseHoldAsync(otherUserId, id);
            }

            // Complete auction immediately
            auction.Status = AuctionStatus.Completed;
            auction.CurrentPrice = auction.BuyoutPrice.Value;
            auction.WinnerId = userId;
            auction.EndReason = "buyout";
            auction.UpdatedAt = DateTime.UtcNow;
            
            await _auctionRepository.UpdateAsync(auction);

            // Tạo Transaction Pending (giữ hold, chờ confirm)
            var buyer = await _userRepository.GetByIdAsync(userId);
            if (buyer != null && bidResult.Bid != null)
            {
                var pendingTransaction = new Transaction
                {
                    UserId = userId,
                    Type = TransactionType.Payment,
                    Amount = -bidResult.Bid.HeldAmount,
                    Description = $"Thanh toán mua ngay - chờ xác nhận",
                    RelatedAuctionId = id,
                    RelatedBidId = bidResult.Bid.Id,
                    Status = TransactionStatus.Pending,
                    BuyerConfirmed = false,
                    SellerConfirmed = false,
                    BalanceBefore = buyer.EscrowBalance,
                    BalanceAfter = buyer.EscrowBalance, // Giữ nguyên vì chưa chuyển
                    CreatedAt = DateTime.UtcNow
                };
                await _transactionRepository.CreateAsync(pendingTransaction);
            }

            // Tạo Order cho giao dịch (để hiển thị trong My Orders / My Sales)
            try
            {
                var productImage = auction.Images?.FirstOrDefault();
                await _orderService.CreateOrderFromAuctionAsync(
                    id,
                    userId,
                    auction.SellerId,
                    auction.BuyoutPrice.Value,
                    auction.Title,
                    productImage
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create order for buyout auction {AuctionId}", id);
            }

            // Notify via SignalR
            await AuctionHub.NotifyAuctionBuyout(_hubContext, id, new
            {
                AuctionId = id,
                AuctionTitle = auction.Title,
                BuyerId = userId,
                BuyerName = buyer?.FullName,
                BuyoutPrice = auction.BuyoutPrice.Value
            });

            // Send Buyout emails to both buyer and seller
            var transactionUrl = $"{_configuration["FrontendUrl"]}/auctions/{auction.Id}";
            
            // Email to buyer
            if (buyer != null && !string.IsNullOrEmpty(buyer.Email))
            {
                try
                {
                    await _emailService.SendBuyoutBuyerEmailAsync(
                        buyer.Email,
                        buyer.FullName,
                        auction.Title,
                        FormatCurrency(auction.BuyoutPrice.Value),
                        transactionUrl);
                    
                    _logger.LogInformation("Sent buyout buyer email to {Email}", buyer.Email);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send buyout buyer email to {Email}", buyer.Email);
                }
            }

            // Email to seller
            var seller = await _userRepository.GetByIdAsync(auction.SellerId);
            if (seller != null && !string.IsNullOrEmpty(seller.Email))
            {
                try
                {
                    await _emailService.SendBuyoutSellerEmailAsync(
                        seller.Email,
                        seller.FullName,
                        auction.Title,
                        FormatCurrency(auction.BuyoutPrice.Value),
                        buyer?.FullName ?? "Người mua",
                        transactionUrl);
                    
                    _logger.LogInformation("Sent buyout seller email to {Email}", seller.Email);
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send buyout seller email to {Email}", seller.Email);
                }
            }

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
            
            // Can only cancel Active auctions
            if (auction.Status != AuctionStatus.Active)
            {
                return BadRequest(new { message = "Can only cancel active auctions" });
            }

            // Nếu có bids, release hold cho tất cả bidders trước khi cancel
            if (bids.Any())
            {
                await _bidService.ReleaseAllHoldsAsync(id);
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

    [HttpPost("{id}/confirm-transaction")]
    public async Task<IActionResult> ConfirmTransaction(string id)
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

            // Xác định user là buyer hay seller
            bool isBuyer = auction.WinnerId == userId;
            bool isSeller = auction.SellerId == userId;

            if (!isBuyer && !isSeller)
            {
                return Forbid();
            }

            // Confirm transaction
            var confirmed = await _bidService.ConfirmTransactionAsync(id, userId, isBuyer);
            if (!confirmed)
            {
                return BadRequest(new { message = "Không thể xác nhận giao dịch. Vui lòng kiểm tra lại trạng thái đấu giá." });
            }

            return Ok(new { message = "Xác nhận giao dịch thành công" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming transaction");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/cancel-transaction")]
    public async Task<IActionResult> CancelTransaction(string id)
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

            // Chỉ buyer hoặc seller mới được cancel
            if (auction.WinnerId != userId && auction.SellerId != userId)
            {
                return Forbid();
            }

            // Cancel transaction và refund
            var cancelled = await _bidService.CancelTransactionAsync(id, userId);
            if (!cancelled)
            {
                return BadRequest(new { message = "Không thể hủy giao dịch. Vui lòng kiểm tra lại trạng thái." });
            }

            return Ok(new { message = "Hủy giao dịch và hoàn tiền thành công" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling transaction");
            return BadRequest(new { message = ex.Message });
        }
    }

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} ₫", amount);
    }
}
