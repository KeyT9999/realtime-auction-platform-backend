using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RealtimeAuction.Api.Dtos.Review;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReviewController : ControllerBase
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewController> _logger;

    public ReviewController(IReviewService reviewService, ILogger<ReviewController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    /// <summary>
    /// Create a review for an order (POST /api/review/{orderId})
    /// </summary>
    [HttpPost("{orderId}")]
    [Authorize]
    public async Task<IActionResult> CreateReview(string orderId, [FromBody] CreateReviewRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Không xác định được người dùng" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _reviewService.CreateReviewAsync(orderId, userId, request);

        if (!result.Success)
        {
            return BadRequest(new { message = result.ErrorMessage });
        }

        return Ok(result);
    }

    /// <summary>
    /// Get user reviews and rating stats (GET /api/review/user/{userId})
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<IActionResult> GetUserReviews(string userId)
    {
        var rating = await _reviewService.GetUserReviewsAsync(userId);
        return Ok(rating);
    }

    /// <summary>
    /// Get reviews for an order (GET /api/review/order/{orderId})
    /// </summary>
    [HttpGet("order/{orderId}")]
    [Authorize]
    public async Task<IActionResult> GetOrderReviews(string orderId)
    {
        var reviews = await _reviewService.GetOrderReviewsAsync(orderId);
        return Ok(reviews);
    }

    /// <summary>
    /// Get user rating stats only (GET /api/review/rating/{userId})
    /// </summary>
    [HttpGet("rating/{userId}")]
    public async Task<IActionResult> GetUserRating(string userId)
    {
        var (avgRating, count) = await _reviewService.GetUserRatingAsync(userId);
        return Ok(new { averageRating = avgRating, totalReviews = count });
    }
}
