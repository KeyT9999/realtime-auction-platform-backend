using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Repositories;

public class ContactMessageRepository : IContactMessageRepository
{
    private readonly IMongoCollection<ContactMessage> _collection;

    public ContactMessageRepository(MongoDbSettings settings)
    {
        var client = new MongoClient(settings.ConnectionString);
        var db = client.GetDatabase(settings.DatabaseName);
        _collection = db.GetCollection<ContactMessage>("ContactMessages");
    }

    public async Task<ContactMessage> CreateAsync(ContactMessage message)
    {
        await _collection.InsertOneAsync(message);
        return message;
    }
}
