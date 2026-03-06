using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IContactMessageRepository
{
    Task<ContactMessage> CreateAsync(ContactMessage message);
}
