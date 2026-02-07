using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Admin;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RequireAdmin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly IAuctionRepository _auctionRepository;
    private readonly IBidRepository _bidRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        IAuctionRepository auctionRepository,
        IBidRepository bidRepository,
        IUserRepository userRepository,
        ICategoryRepository categoryRepository,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _auctionRepository = auctionRepository;
        _bidRepository = bidRepository;
        _userRepository = userRepository;
        _categoryRepository = categoryRepository;
        _logger = logger;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isLocked = null)
    {
        try
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 10;

            var response = await _adminService.GetUsersAsync(page, pageSize, search, role, isLocked);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("users/{id}")]
    public async Task<IActionResult> GetUserById(string id)
    {
        try
        {
            var user = await _adminService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user by ID");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var user = await _adminService.CreateUserAsync(request);
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("users/{id}")]
    public async Task<IActionResult> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = await _adminService.UpdateUserAsync(id, request);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user");
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new { message = ex.Message });
            }
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("users/{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        try
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentAdminId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var result = await _adminService.DeleteUserAsync(id, currentAdminId);
            if (result)
            {
                return Ok(new { message = "User deleted successfully" });
            }
            return NotFound(new { message = "User not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("users/{id}/lock")]
    public async Task<IActionResult> LockUser(string id, [FromBody] LockUserRequest request)
    {
        try
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentAdminId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var user = await _adminService.LockUserAsync(id, request, currentAdminId);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking user");
            if (ex.Message.Contains("not found") || ex.Message.Contains("already locked"))
            {
                return BadRequest(new { message = ex.Message });
            }
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("users/{id}/unlock")]
    public async Task<IActionResult> UnlockUser(string id)
    {
        try
        {
            var user = await _adminService.UnlockUserAsync(id);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking user");
            if (ex.Message.Contains("not found") || ex.Message.Contains("not locked"))
            {
                return BadRequest(new { message = ex.Message });
            }
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("users/{id}/role")]
    public async Task<IActionResult> ChangeUserRole(string id, [FromBody] ChangeRoleRequest request)
    {
        try
        {
            var user = await _adminService.ChangeUserRoleAsync(id, request);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing user role");
            if (ex.Message.Contains("not found"))
            {
                return NotFound(new { message = ex.Message });
            }
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        try
        {
            var stats = await _adminService.GetUserStatsAsync();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting stats");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard/stats")]
    public async Task<IActionResult> GetDashboardStats()
    {
        try
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var weekAgo = today.AddDays(-7);
            var hourAgo = now.AddHours(-1);
            var activeThreshold = now.AddMinutes(-15); // Consider users active if activity in last 15 min

            var allUsers = await _userRepository.GetAllAsync();
            var allAuctions = await _auctionRepository.GetAllAsync();
            var allBids = await _bidRepository.GetAllAsync();

            var totalUsers = allUsers.Count;
            var newUsersToday = allUsers.Count(u => u.CreatedAt >= today);
            var activeAuctions = allAuctions.Count(a => a.Status == AuctionStatus.Active);
            var completedToday = allAuctions.Where(a => 
                a.Status == AuctionStatus.Completed && 
                a.UpdatedAt >= today).ToList();
            var todayRevenue = completedToday.Sum(a => a.CurrentPrice);
            var bidsLastHour = allBids.Count(b => b.Timestamp >= hourAgo);

            // Calculate growth trends
            var usersYesterday = allUsers.Count(u => u.CreatedAt < today);
            var usersWeekAgo = allUsers.Count(u => u.CreatedAt < weekAgo);
            var auctionsYesterday = allAuctions.Count(a => a.CreatedAt < today);
            var revenueYesterday = allAuctions
                .Where(a => a.Status == AuctionStatus.Completed && 
                           a.UpdatedAt >= yesterday && a.UpdatedAt < today)
                .Sum(a => a.CurrentPrice);

            var userGrowthDay = usersYesterday > 0 ? (decimal)(newUsersToday) / usersYesterday * 100 : 0;
            var userGrowthWeek = usersWeekAgo > 0 ? (decimal)(totalUsers - usersWeekAgo) / usersWeekAgo * 100 : 0;
            var auctionGrowthDay = auctionsYesterday > 0 ? (decimal)(allAuctions.Count - auctionsYesterday) / auctionsYesterday * 100 : 0;
            var revenueGrowthDay = revenueYesterday > 0 ? (todayRevenue - revenueYesterday) / revenueYesterday * 100 : 0;

            var stats = new DashboardStatsDto
            {
                TotalUsers = totalUsers,
                UsersOnline = 0, // TODO: Implement via SignalR tracking
                NewUsersToday = newUsersToday,
                ActiveAuctions = activeAuctions,
                CompletedAuctionsToday = completedToday.Count,
                TodayRevenue = todayRevenue,
                BidsInLastHour = bidsLastHour,
                Trends = new GrowthTrends
                {
                    UserGrowthDay = Math.Round(userGrowthDay, 2),
                    UserGrowthWeek = Math.Round(userGrowthWeek, 2),
                    AuctionGrowthDay = Math.Round(auctionGrowthDay, 2),
                    RevenueGrowthDay = Math.Round(revenueGrowthDay, 2)
                }
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard stats");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard/charts")]
    public async Task<IActionResult> GetDashboardCharts()
    {
        try
        {
            var now = DateTime.UtcNow;
            var thirtyDaysAgo = now.AddDays(-30);

            var allAuctions = await _auctionRepository.GetAllAsync();
            var allUsers = await _userRepository.GetAllAsync();
            var allCategories = await _categoryRepository.GetAllAsync();

            // Revenue chart (last 30 days)
            var revenueData = new List<RevenueDataPoint>();
            for (int i = 29; i >= 0; i--)
            {
                var date = now.AddDays(-i).Date;
                var nextDate = date.AddDays(1);
                var dayAuctions = allAuctions.Where(a => 
                    a.Status == AuctionStatus.Completed &&
                    a.UpdatedAt >= date && a.UpdatedAt < nextDate).ToList();
                
                revenueData.Add(new RevenueDataPoint
                {
                    Date = date.ToString("MM/dd"),
                    Revenue = dayAuctions.Sum(a => a.CurrentPrice),
                    AuctionsCompleted = dayAuctions.Count
                });
            }

            // User growth chart (last 30 days)
            var userGrowthData = new List<UserGrowthDataPoint>();
            for (int i = 29; i >= 0; i--)
            {
                var date = now.AddDays(-i).Date;
                var nextDate = date.AddDays(1);
                var totalUpToDate = allUsers.Count(u => u.CreatedAt < nextDate);
                var newOnDate = allUsers.Count(u => u.CreatedAt >= date && u.CreatedAt < nextDate);
                
                userGrowthData.Add(new UserGrowthDataPoint
                {
                    Date = date.ToString("MM/dd"),
                    TotalUsers = totalUpToDate,
                    NewUsers = newOnDate
                });
            }

            // Category distribution
            var categoryData = new List<CategoryDataPoint>();
            foreach (var category in allCategories)
            {
                var categoryAuctions = allAuctions.Where(a => a.CategoryId == category.Id).ToList();
                var completedAuctions = categoryAuctions.Where(a => a.Status == AuctionStatus.Completed).ToList();
                
                categoryData.Add(new CategoryDataPoint
                {
                    CategoryName = category.Name,
                    AuctionCount = categoryAuctions.Count,
                    TotalRevenue = completedAuctions.Sum(a => a.CurrentPrice)
                });
            }

            // Auction completion rate
            var completed = allAuctions.Count(a => a.Status == AuctionStatus.Completed);
            var cancelled = allAuctions.Count(a => a.Status == AuctionStatus.Cancelled);
            var active = allAuctions.Count(a => a.Status == AuctionStatus.Active);
            var total = completed + cancelled;
            var completionRate = total > 0 ? (decimal)completed / total * 100 : 0;

            var charts = new DashboardChartsDto
            {
                RevenueChart = revenueData,
                UserGrowthChart = userGrowthData,
                CategoryDistribution = categoryData.OrderByDescending(c => c.AuctionCount).Take(10).ToList(),
                AuctionCompletion = new AuctionCompletionRateDto
                {
                    Completed = completed,
                    Cancelled = cancelled,
                    Active = active,
                    CompletionRate = Math.Round(completionRate, 2)
                }
            };

            return Ok(charts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard charts");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard/activities")]
    public async Task<IActionResult> GetDashboardActivities()
    {
        try
        {
            var allBids = await _bidRepository.GetAllAsync();
            var allAuctions = await _auctionRepository.GetAllAsync();
            var allUsers = await _userRepository.GetAllAsync();

            // Recent bids (last 20)
            var recentBids = allBids.OrderByDescending(b => b.Timestamp).Take(20)
                .Select(async b =>
                {
                    var auction = await _auctionRepository.GetByIdAsync(b.AuctionId);
                    var user = await _userRepository.GetByIdAsync(b.UserId);
                    return new ActivityItem
                    {
                        Id = b.Id ?? "",
                        Type = "bid",
                        Title = auction?.Title ?? "Unknown auction",
                        Description = $"{user?.FullName ?? "Anonymous"} đặt giá {b.Amount:N0} VND",
                        Timestamp = b.Timestamp,
                        UserName = user?.FullName,
                        Amount = b.Amount
                    };
                }).Select(t => t.Result).ToList();

            // New auctions (last 10)
            var newAuctions = allAuctions.OrderByDescending(a => a.CreatedAt).Take(10)
                .Select(async a =>
                {
                    var seller = await _userRepository.GetByIdAsync(a.SellerId);
                    return new ActivityItem
                    {
                        Id = a.Id ?? "",
                        Type = "auction",
                        Title = a.Title,
                        Description = $"Bởi {seller?.FullName ?? "Unknown"} - Giá khởi điểm: {a.StartingPrice:N0} VND",
                        Timestamp = a.CreatedAt,
                        UserName = seller?.FullName,
                        Amount = a.StartingPrice
                    };
                }).Select(t => t.Result).ToList();

            // New users (last 10)
            var newUsers = allUsers.OrderByDescending(u => u.CreatedAt).Take(10)
                .Select(u => new ActivityItem
                {
                    Id = u.Id ?? "",
                    Type = "user",
                    Title = u.FullName,
                    Description = $"Đăng ký tài khoản - Email: {u.Email}",
                    Timestamp = u.CreatedAt,
                    UserName = u.FullName
                }).ToList();

            // Completed auctions (last 10)
            var completedAuctions = allAuctions
                .Where(a => a.Status == AuctionStatus.Completed)
                .OrderByDescending(a => a.UpdatedAt).Take(10)
                .Select(async a =>
                {
                    var seller = await _userRepository.GetByIdAsync(a.SellerId);
                    var winner = !string.IsNullOrEmpty(a.WinnerId) ? await _userRepository.GetByIdAsync(a.WinnerId) : null;
                    return new ActivityItem
                    {
                        Id = a.Id ?? "",
                        Type = "auction_completed",
                        Title = a.Title,
                        Description = $"Kết thúc - Người thắng: {winner?.FullName ?? "N/A"} - Giá: {a.CurrentPrice:N0} VND",
                        Timestamp = a.UpdatedAt,
                        UserName = winner?.FullName,
                        Amount = a.CurrentPrice
                    };
                }).Select(t => t.Result).ToList();

            var activities = new DashboardActivitiesDto
            {
                RecentBids = recentBids,
                NewAuctions = newAuctions,
                NewUsers = newUsers,
                CompletedAuctions = completedAuctions
            };

            return Ok(activities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard activities");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard/alerts")]
    public async Task<IActionResult> GetDashboardAlerts()
    {
        try
        {
            var alerts = new List<SystemAlert>();
            var now = DateTime.UtcNow;

            var allUsers = await _userRepository.GetAllAsync();
            var allAuctions = await _auctionRepository.GetAllAsync();

            // Check for locked users
            var lockedUsers = allUsers.Where(u => u.IsLocked).ToList();
            if (lockedUsers.Count > 0)
            {
                alerts.Add(new SystemAlert
                {
                    Type = "warning",
                    Message = $"{lockedUsers.Count} người dùng đang bị khóa",
                    EntityType = "User",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Check for expired auctions without winner
            var expiredNoWinner = allAuctions.Where(a => 
                a.Status == AuctionStatus.Active && 
                a.EndTime < now).ToList();
            if (expiredNoWinner.Count > 0)
            {
                alerts.Add(new SystemAlert
                {
                    Type = "critical",
                    Message = $"{expiredNoWinner.Count} đấu giá hết hạn cần xử lý",
                    EntityType = "Auction",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Check for users with negative balance (shouldn't happen but check)
            var negativeBalance = allUsers.Where(u => u.AvailableBalance < 0 || u.EscrowBalance < 0).ToList();
            if (negativeBalance.Count > 0)
            {
                alerts.Add(new SystemAlert
                {
                    Type = "critical",
                    Message = $"{negativeBalance.Count} người dùng có số dư âm - cần kiểm tra!",
                    EntityType = "User",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Check for auctions ending soon without bids
            var endingSoonNoBids = allAuctions.Where(a => 
                a.Status == AuctionStatus.Active &&
                a.EndTime > now && a.EndTime < now.AddHours(2) &&
                a.BidCount == 0).ToList();
            if (endingSoonNoBids.Count > 0)
            {
                alerts.Add(new SystemAlert
                {
                    Type = "info",
                    Message = $"{endingSoonNoBids.Count} đấu giá sắp kết thúc chưa có bid",
                    EntityType = "Auction",
                    Timestamp = DateTime.UtcNow
                });
            }

            var result = new DashboardAlertsDto
            {
                Alerts = alerts.OrderByDescending(a => a.Type == "critical" ? 3 : a.Type == "warning" ? 2 : 1)
                              .ThenByDescending(a => a.Timestamp).ToList(),
                TotalAlerts = alerts.Count,
                CriticalAlerts = alerts.Count(a => a.Type == "critical"),
                WarningAlerts = alerts.Count(a => a.Type == "warning")
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting dashboard alerts");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("users/bulk-lock")]
    public async Task<IActionResult> BulkLockUsers([FromBody] BulkUserActionRequest request)
    {
        try
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentAdminId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var count = await _adminService.BulkLockUsersAsync(request.UserIds, request.Reason, currentAdminId);
            return Ok(new { message = $"Successfully locked {count} users", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk locking users");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("users/bulk-unlock")]
    public async Task<IActionResult> BulkUnlockUsers([FromBody] BulkUserActionRequest request)
    {
        try
        {
            var count = await _adminService.BulkUnlockUsersAsync(request.UserIds);
            return Ok(new { message = $"Successfully unlocked {count} users", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk unlocking users");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("users/bulk-delete")]
    public async Task<IActionResult> BulkDeleteUsers([FromBody] BulkUserActionRequest request)
    {
        try
        {
            var currentAdminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(currentAdminId))
            {
                return Unauthorized(new { message = "User not authenticated" });
            }

            var count = await _adminService.BulkDeleteUsersAsync(request.UserIds, currentAdminId);
            return Ok(new { message = $"Successfully deleted {count} users", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk deleting users");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("users/bulk-role")]
    public async Task<IActionResult> BulkChangeRole([FromBody] BulkChangeRoleRequest request)
    {
        try
        {
            var count = await _adminService.BulkChangeRoleAsync(request.UserIds, request.Role);
            return Ok(new { message = $"Successfully changed role for {count} users", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk changing user roles");
            return BadRequest(new { message = ex.Message });
        }
    }
    
    // User detail endpoints
    [HttpGet("users/{id}/auctions")]
    public async Task<IActionResult> GetUserAuctions(string id)
    {
        try
        {
            var auctions = await _adminService.GetUserAuctionsAsync(id);
            return Ok(auctions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user auctions");
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpGet("users/{id}/bids")]
    public async Task<IActionResult> GetUserBids(string id)
    {
        try
        {
            var bids = await _adminService.GetUserBidsAsync(id);
            return Ok(bids);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user bids");
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpGet("users/{id}/transactions")]
    public async Task<IActionResult> GetUserTransactions(string id)
    {
        try
        {
            var transactions = await _adminService.GetUserTransactionsAsync(id);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user transactions");
            return BadRequest(new { message = ex.Message });
        }
    }
    
    // Balance management endpoints
    [HttpPost("users/{id}/balance/add")]
    public async Task<IActionResult> AddBalance(string id, [FromBody] AddBalanceRequest request)
    {
        try
        {
            var user = await _adminService.AddBalanceAsync(id, request);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding balance");
            return BadRequest(new { message = ex.Message });
        }
    }
    
    [HttpPost("users/{id}/balance/subtract")]
    public async Task<IActionResult> SubtractBalance(string id, [FromBody] SubtractBalanceRequest request)
    {
        try
        {
            var user = await _adminService.SubtractBalanceAsync(id, request);
            return Ok(user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subtracting balance");
            return BadRequest(new { message = ex.Message });
        }
    }
}
