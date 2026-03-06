using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;
using System.Security.Claims;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/contact")]
public class ContactController : ControllerBase
{
    private readonly IContactMessageRepository _repo;
    private readonly ILogger<ContactController> _logger;

    public ContactController(IContactMessageRepository repo, ILogger<ContactController> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    /// <summary>
    /// Submit contact form (no auth required for guests).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] ContactRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request?.Name) || string.IsNullOrWhiteSpace(request.Email) ||
                string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Vui lòng điền đầy đủ thông tin." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var message = new ContactMessage
            {
                Name = request.Name.Trim(),
                Email = request.Email.Trim(),
                Subject = request.Subject.Trim(),
                Message = request.Message.Trim(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            await _repo.CreateAsync(message);
            _logger.LogInformation("Contact message from {Email}, subject: {Subject}", message.Email, message.Subject);

            return Ok(new { success = true, message = "Cảm ơn bạn! Chúng tôi sẽ phản hồi sớm." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting contact");
            return StatusCode(500, new { message = "Có lỗi xảy ra. Vui lòng thử lại sau." });
        }
    }
}

public class ContactRequest
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Message { get; set; } = "";
}
