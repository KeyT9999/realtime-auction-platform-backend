using System.ComponentModel.DataAnnotations;

namespace RealtimeAuction.Api.Dtos.Review;

public class CreateReviewRequest
{
    [Required]
    [Range(1, 5, ErrorMessage = "Rating phải từ 1 đến 5 sao")]
    public int Rating { get; set; }

    [MaxLength(500, ErrorMessage = "Nội dung đánh giá tối đa 500 ký tự")]
    public string? Comment { get; set; }
}

public class ReviewDto
{
    public string Id { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public string ReviewerId { get; set; } = null!;
    public string RevieweeId { get; set; } = null!;
    public string? ReviewerName { get; set; }
    public string? RevieweeName { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Product info for context
    public string? ProductTitle { get; set; }
}

public class UserRatingDto
{
    public string UserId { get; set; } = null!;
    public string? UserName { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public List<ReviewDto> RecentReviews { get; set; } = new();
}

public class ReviewResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ReviewDto? Review { get; set; }
}
