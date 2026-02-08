using RealtimeAuction.Api.Dtos.Review;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services;

public interface IReviewService
{
    Task<ReviewResult> CreateReviewAsync(string orderId, string userId, CreateReviewRequest request);
    Task<UserRatingDto> GetUserReviewsAsync(string userId);
    Task<(double avgRating, int count)> GetUserRatingAsync(string userId);
    Task<IEnumerable<ReviewDto>> GetOrderReviewsAsync(string orderId);
}
