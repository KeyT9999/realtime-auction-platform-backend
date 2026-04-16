// Mục đích tệp: Truy cap va thao tac du lieu cho phan IContactMessageRepository.
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public interface IContactMessageRepository
{
    Task<ContactMessage> CreateAsync(ContactMessage message);
}
