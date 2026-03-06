using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly IMongoCollection<Notification> _collection;

    public NotificationRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var db = client.GetDatabase(settings.DatabaseName);
        _collection = db.GetCollection<Notification>("Notifications");
    }

    public async Task<Notification> CreateAsync(Notification notification)
    {
        await _collection.InsertOneAsync(notification);
        return notification;
    }

    public async Task<List<Notification>> GetByUserIdAsync(string userId, int page = 1, int limit = 20)
    {
        return await _collection
            .Find(n => n.UserId == userId)
            .SortByDescending(n => n.CreatedAt)
            .Skip((page - 1) * limit)
            .Limit(limit)
            .ToListAsync();
    }

    public async Task<int> CountUnreadByUserIdAsync(string userId)
    {
        return (int)await _collection.CountDocumentsAsync(
            Builders<Notification>.Filter.And(
                Builders<Notification>.Filter.Eq(n => n.UserId, userId),
                Builders<Notification>.Filter.Eq(n => n.IsRead, false)));
    }

    public async Task MarkAsReadAsync(string notificationId, string userId)
    {
        await _collection.UpdateOneAsync(
            n => n.Id == notificationId && n.UserId == userId,
            Builders<Notification>.Update.Set(n => n.IsRead, true));
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        await _collection.UpdateManyAsync(
            n => n.UserId == userId,
            Builders<Notification>.Update.Set(n => n.IsRead, true));
    }
}
