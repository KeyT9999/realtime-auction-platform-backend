using Microsoft.AspNetCore.Mvc;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Backend connected successfully!", timestamp = DateTime.UtcNow });
    }

    [HttpGet("ping")]
    public IActionResult Ping()
    {
        return Ok(new { status = "pong", message = "API is working!" });
    }
}
