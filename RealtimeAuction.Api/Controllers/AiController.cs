using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IGeminiService _geminiService;
    private readonly ILogger<AiController> _logger;

    public AiController(IGeminiService geminiService, ILogger<AiController> logger)
    {
        _geminiService = geminiService;
        _logger = logger;
    }

    [HttpPost("analyze-product-images")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> AnalyzeProductImages([FromForm(Name = "images")] List<IFormFile>? images, CancellationToken cancellationToken)
    {
        if (images == null || images.Count == 0)
        {
            return BadRequest(new { message = "Vui lòng tải lên ít nhất một ảnh." });
        }

        if (images.Count > 5)
        {
            return BadRequest(new { message = "Tối đa 5 ảnh cho mỗi lần phân tích." });
        }

        const long maxBytes = 10 * 1024 * 1024;
        if (images.Any(file => file.Length <= 0 || file.Length > maxBytes))
        {
            return BadRequest(new { message = "Mỗi ảnh phải lớn hơn 0B và không vượt quá 10MB." });
        }

        try
        {
            var json = await _geminiService.AnalyzeProductImagesAsync(images, cancellationToken);
            using var document = JsonDocument.Parse(json);
            return Ok(document.RootElement.Clone());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "AI image analysis failed due to configuration or upstream response.");
            return StatusCode(503, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while analyzing product images.");
            return BadRequest(new { message = "Không thể phân tích ảnh sản phẩm lúc này." });
        }
    }
}
