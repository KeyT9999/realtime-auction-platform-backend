using RealtimeAuction.Api.Dtos.Review;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services;

public class ReviewService : IReviewService
{
    private readonly IReviewRepository _reviewRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        IReviewRepository reviewRepository,
        IOrderRepository orderRepository,
        IUserRepository userRepository,
        ILogger<ReviewService> logger)
    {
        _reviewRepository = reviewRepository;
        _orderRepository = orderRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ReviewResult> CreateReviewAsync(string orderId, string userId, CreateReviewRequest request)
    {
        // 1. Get the order
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            return new ReviewResult { Success = false, ErrorMessage = "Không tìm thấy đơn hàng" };
        }

        // 2. Validate order is completed
        if (order.Status != OrderStatus.Completed)
        {
            return new ReviewResult { Success = false, ErrorMessage = "Chỉ có thể đánh giá khi đơn hàng đã hoàn thành" };
        }

        // 3. Determine if user is buyer or seller
        bool isBuyer = order.BuyerId == userId;
        bool isSeller = order.SellerId == userId;

        if (!isBuyer && !isSeller)
        {
            return new ReviewResult { Success = false, ErrorMessage = "Bạn không có quyền đánh giá đơn hàng này" };
        }

        // 4. Check if already reviewed
        if (isBuyer && order.BuyerHasReviewed)
        {
            return new ReviewResult { Success = false, ErrorMessage = "Bạn đã đánh giá người bán rồi" };
        }
        if (isSeller && order.SellerHasReviewed)
        {
            return new ReviewResult { Success = false, ErrorMessage = "Bạn đã đánh giá người mua rồi" };
        }

        // 5. Check for existing review (double check)
        var existingReview = await _reviewRepository.GetByOrderAndReviewerAsync(orderId, userId);
        if (existingReview != null)
        {
            return new ReviewResult { Success = false, ErrorMessage = "Bạn đã đánh giá đơn hàng này rồi" };
        }

        // 6. Determine reviewee (the other party)
        string revieweeId = isBuyer ? order.SellerId : order.BuyerId;

        // 7. Validate no self-review
        if (userId == revieweeId)
        {
            return new ReviewResult { Success = false, ErrorMessage = "Không thể tự đánh giá chính mình" };
        }

        // 8. Create the review
        var review = new Review
        {
            OrderId = orderId,
            ReviewerId = userId,
            RevieweeId = revieweeId,
            Rating = request.Rating,
            Comment = request.Comment?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        await _reviewRepository.CreateAsync(review);

        // 9. Update order review status
        if (isBuyer)
        {
            order.BuyerHasReviewed = true;
        }
        else
        {
            order.SellerHasReviewed = true;
        }
        order.UpdatedAt = DateTime.UtcNow;
        await _orderRepository.UpdateAsync(order);

        _logger.LogInformation("Review created: {ReviewId} by {UserId} for order {OrderId}", 
            review.Id, userId, orderId);

        return new ReviewResult 
        { 
            Success = true, 
            Review = await MapToReviewDtoAsync(review, order.ProductTitle)
        };
    }

    public async Task<UserRatingDto> GetUserReviewsAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        var reviews = await _reviewRepository.GetByRevieweeIdAsync(userId);
        var (avgRating, count) = await _reviewRepository.GetUserRatingStatsAsync(userId);

        var reviewDtos = new List<ReviewDto>();
        foreach (var review in reviews.Take(10)) // Only recent 10 reviews
        {
            reviewDtos.Add(await MapToReviewDtoAsync(review));
        }

        return new UserRatingDto
        {
            UserId = userId,
            UserName = user?.FullName,
            AverageRating = avgRating,
            TotalReviews = count,
            RecentReviews = reviewDtos
        };
    }

    public async Task<(double avgRating, int count)> GetUserRatingAsync(string userId)
    {
        return await _reviewRepository.GetUserRatingStatsAsync(userId);
    }

    public async Task<IEnumerable<ReviewDto>> GetOrderReviewsAsync(string orderId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        var reviews = await _reviewRepository.GetByOrderIdAsync(orderId);
        
        var reviewDtos = new List<ReviewDto>();
        foreach (var review in reviews)
        {
            reviewDtos.Add(await MapToReviewDtoAsync(review, order?.ProductTitle));
        }
        return reviewDtos;
    }

    private async Task<ReviewDto> MapToReviewDtoAsync(Review review, string? productTitle = null)
    {
        var reviewer = await _userRepository.GetByIdAsync(review.ReviewerId);
        var reviewee = await _userRepository.GetByIdAsync(review.RevieweeId);

        return new ReviewDto
        {
            Id = review.Id!,
            OrderId = review.OrderId,
            ReviewerId = review.ReviewerId,
            RevieweeId = review.RevieweeId,
            ReviewerName = reviewer?.FullName,
            RevieweeName = reviewee?.FullName,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = review.CreatedAt,
            ProductTitle = productTitle
        };
    }
}
