using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(string id);
    Task<Review?> GetByOrderAndReviewerAsync(string orderId, string reviewerId);
    Task<IEnumerable<Review>> GetByRevieweeIdAsync(string userId);
    Task<IEnumerable<Review>> GetByOrderIdAsync(string orderId);
    Task<Review> CreateAsync(Review review);
    Task<(double avgRating, int count)> GetUserRatingStatsAsync(string userId);
}
