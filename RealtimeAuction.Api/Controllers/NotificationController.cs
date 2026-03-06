using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationRepository _repo;
    private readonly ILogger<NotificationController> _logger;

    public NotificationController(INotificationRepository repo, ILogger<NotificationController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
        try
        {
            var userId = GetUserId();
            var list = await _repo.GetByUserIdAsync(userId, page, limit);
            var unreadCount = await _repo.CountUnreadByUserIdAsync(userId);
            return Ok(new { notifications = list, unreadCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        try
        {
            await _repo.MarkAsReadAsync(id, GetUserId());
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification read");
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        try
        {
            await _repo.MarkAllAsReadAsync(GetUserId());
            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all read");
            return BadRequest(new { message = ex.Message });
        }
    }
}
