using MongoDB.Driver;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly IMongoCollection<Review> _reviews;

    public ReviewRepository(IMongoDatabase database)
    {
        _reviews = database.GetCollection<Review>("Reviews");
    }

    public async Task<Review?> GetByIdAsync(string id)
    {
        return await _reviews.Find(r => r.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Review?> GetByOrderAndReviewerAsync(string orderId, string reviewerId)
    {
        return await _reviews.Find(r => r.OrderId == orderId && r.ReviewerId == reviewerId).FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<Review>> GetByRevieweeIdAsync(string userId)
    {
        return await _reviews.Find(r => r.RevieweeId == userId)
            .SortByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Review>> GetByOrderIdAsync(string orderId)
    {
        return await _reviews.Find(r => r.OrderId == orderId).ToListAsync();
    }

    public async Task<Review> CreateAsync(Review review)
    {
        await _reviews.InsertOneAsync(review);
        return review;
    }

    public async Task<(double avgRating, int count)> GetUserRatingStatsAsync(string userId)
    {
        var reviews = await _reviews.Find(r => r.RevieweeId == userId).ToListAsync();
        
        if (reviews.Count == 0)
        {
            return (0, 0);
        }

        var avgRating = reviews.Average(r => r.Rating);
        return (Math.Round(avgRating, 1), reviews.Count);
    }
}
