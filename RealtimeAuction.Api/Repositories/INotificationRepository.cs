using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface INotificationRepository
{
    Task<Notification> CreateAsync(Notification notification);
    Task<List<Notification>> GetByUserIdAsync(string userId, int page = 1, int limit = 20);
    Task<int> CountUnreadByUserIdAsync(string userId);
    Task MarkAsReadAsync(string notificationId, string userId);
    Task MarkAllAsReadAsync(string userId);
}
