using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RealtimeAuction.Api.Settings;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IFirebaseTokenService _firebaseTokenService;
    private readonly FirebaseAuthSettings _firebaseAuthSettings;

    public ChatController(
        IFirebaseTokenService firebaseTokenService,
        IOptions<FirebaseAuthSettings> firebaseAuthSettings)
    {
        _firebaseTokenService = firebaseTokenService;
        _firebaseAuthSettings = firebaseAuthSettings.Value;
    }

    [HttpGet("firebase-token")]
    public async Task<IActionResult> GetFirebaseToken(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new { message = "User not authenticated." });
        }

        var claims = new Dictionary<string, object?>
        {
            ["role"] = User.FindFirstValue(ClaimTypes.Role),
            ["email"] = User.FindFirstValue(ClaimTypes.Email),
            ["name"] = User.FindFirstValue(ClaimTypes.Name)
        };

        try
        {
            var token = await _firebaseTokenService.CreateCustomTokenAsync(userId, claims, cancellationToken);
            return Ok(new
            {
                token,
                projectId = _firebaseAuthSettings.ProjectId
            });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
    }
}
