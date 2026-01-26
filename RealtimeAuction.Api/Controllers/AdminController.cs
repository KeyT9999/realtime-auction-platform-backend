using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Dtos.Admin;
using RealtimeAuction.Api.Services;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = "RequireAdmin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService adminService, ILogger<AdminController> logger)
    {
        _adminService = adminService;
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
}
