using MongoDB.Driver;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Repositories;

public class ContactMessageRepository : IContactMessageRepository
{
    private readonly IMongoCollection<ContactMessage> _collection;

    public ContactMessageRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ContactMessage>("ContactMessages");
    }

    public async Task<ContactMessage> CreateAsync(ContactMessage message)
    {
        await _collection.InsertOneAsync(message);
        return message;
    }
}
