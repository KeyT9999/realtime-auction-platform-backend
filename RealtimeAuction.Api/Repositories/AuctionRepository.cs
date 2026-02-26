using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Repositories;

public class AuctionRepository : IAuctionRepository
{
    private readonly IMongoCollection<Auction> _auctions;

    public AuctionRepository(IMongoDatabase database)
    {
        _auctions = database.GetCollection<Auction>("Auctions");
    }

    public async Task<Auction?> GetByIdAsync(string id)
    {
        return await _auctions.Find(a => a.Id == id).FirstOrDefaultAsync();
    }

    public async Task<Auction> CreateAsync(Auction auction)
    {
        auction.CreatedAt = DateTime.UtcNow;
        auction.UpdatedAt = DateTime.UtcNow;
        await _auctions.InsertOneAsync(auction);
        return auction;
    }

    public async Task<Auction> UpdateAsync(Auction auction)
    {
        auction.UpdatedAt = DateTime.UtcNow;
        await _auctions.ReplaceOneAsync(a => a.Id == auction.Id, auction);
        return auction;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var result = await _auctions.DeleteOneAsync(a => a.Id == id);
        return result.DeletedCount > 0;
    }

    public async Task<List<Auction>> GetAllAsync()
    {
        return await _auctions.Find(_ => true).ToListAsync();
    }

    public async Task<List<Auction>> GetByStatusAsync(AuctionStatus status)
    {
        return await _auctions.Find(a => a.Status == status).ToListAsync();
    }

    public async Task<List<Auction>> GetBySellerIdAsync(string sellerId)
    {
        return await _auctions.Find(a => a.SellerId == sellerId).ToListAsync();
    }

    public async Task<List<Auction>> GetByCategoryIdAsync(string categoryId)
    {
        return await _auctions.Find(a => a.CategoryId == categoryId).ToListAsync();
    }

    public async Task<List<Auction>> GetActiveAuctionsAsync()
    {
        var now = DateTime.UtcNow;
        return await _auctions.Find(a => 
            a.Status == AuctionStatus.Active && 
            a.StartTime <= now && 
            a.EndTime >= now).ToListAsync();
    }

    public async Task<Auction> UpdateCurrentPriceAsync(string auctionId, decimal newPrice)
    {
        var update = Builders<Auction>.Update
            .Set(a => a.CurrentPrice, newPrice)
            .Set(a => a.UpdatedAt, DateTime.UtcNow);

        await _auctions.UpdateOneAsync(a => a.Id == auctionId, update);
        
        var updatedAuction = await GetByIdAsync(auctionId);
        if (updatedAuction == null)
        {
            throw new Exception($"Auction with id {auctionId} not found after update");
        }
        return updatedAuction;
    }

    public async Task<bool> ValidateStatusTransitionAsync(AuctionStatus currentStatus, AuctionStatus newStatus)
    {
        // If status is the same, it's valid
        if (currentStatus == newStatus)
        {
            return true;
        }

        // Define valid transitions
        return currentStatus switch
        {
            AuctionStatus.Active => newStatus == AuctionStatus.Pending || newStatus == AuctionStatus.Cancelled,
            AuctionStatus.Pending => newStatus == AuctionStatus.Completed || newStatus == AuctionStatus.Cancelled,
            AuctionStatus.Completed => false, // Cannot transition from Completed
            AuctionStatus.Cancelled => false, // Cannot transition from Cancelled
            _ => false
        };
    }

    public async Task<(List<Auction> items, int totalCount)> SearchAuctionsAsync(
        string? keyword,
        AuctionStatus? status,
        string? categoryId,
        decimal? minPrice,
        decimal? maxPrice,
        string? timeFilter,
        string sortBy,
        string sortOrder,
        int page,
        int pageSize)
    {
        var filterBuilder = Builders<Auction>.Filter;
        var filters = new List<FilterDefinition<Auction>>();

        // Keyword search (title or description)
        if (!string.IsNullOrEmpty(keyword))
        {
            var keywordFilter = filterBuilder.Or(
                filterBuilder.Regex(a => a.Title, new MongoDB.Bson.BsonRegularExpression(keyword, "i")),
                filterBuilder.Regex(a => a.Description, new MongoDB.Bson.BsonRegularExpression(keyword, "i"))
            );
            filters.Add(keywordFilter);
        }

        // Status filter
        if (status.HasValue)
        {
            filters.Add(filterBuilder.Eq(a => a.Status, status.Value));
        }

        // Category filter
        if (!string.IsNullOrEmpty(categoryId))
        {
            filters.Add(filterBuilder.Eq(a => a.CategoryId, categoryId));
        }

        // Price range filter
        if (minPrice.HasValue)
        {
            filters.Add(filterBuilder.Gte(a => a.CurrentPrice, minPrice.Value));
        }
        if (maxPrice.HasValue)
        {
            filters.Add(filterBuilder.Lte(a => a.CurrentPrice, maxPrice.Value));
        }

        // Time-based filters
        var now = DateTime.UtcNow;
        if (!string.IsNullOrEmpty(timeFilter))
        {
            switch (timeFilter.ToLower())
            {
                case "upcoming":
                    // StartTime > now (chưa bắt đầu)
                    filters.Add(filterBuilder.Gt(a => a.StartTime, now));
                    break;
                case "ending_soon":
                    // EndTime < now + 24h AND EndTime > now (sắp kết thúc trong 24h)
                    var next24h = now.AddHours(24);
                    filters.Add(filterBuilder.And(
                        filterBuilder.Lt(a => a.EndTime, next24h),
                        filterBuilder.Gt(a => a.EndTime, now)
                    ));
                    break;
                case "new":
                    // CreatedAt > now - 24h (mới đăng trong 24h)
                    var last24h = now.AddHours(-24);
                    filters.Add(filterBuilder.Gt(a => a.CreatedAt, last24h));
                    break;
            }
        }


        // Combine all filters
        var combinedFilter = filters.Count > 0 
            ? filterBuilder.And(filters) 
            : filterBuilder.Empty;

        // Get total count
        var totalCount = (int)await _auctions.CountDocumentsAsync(combinedFilter);

        // Sorting
        var sortDefinition = sortBy.ToLower() switch
        {
            "currentprice" => sortOrder.ToLower() == "asc" 
                ? Builders<Auction>.Sort.Ascending(a => a.CurrentPrice)
                : Builders<Auction>.Sort.Descending(a => a.CurrentPrice),
            "endtime" => sortOrder.ToLower() == "asc"
                ? Builders<Auction>.Sort.Ascending(a => a.EndTime)
                : Builders<Auction>.Sort.Descending(a => a.EndTime),
            "popular" => Builders<Auction>.Sort.Descending(a => a.CurrentPrice), // Popular by price for now
            _ => sortOrder.ToLower() == "asc"
                ? Builders<Auction>.Sort.Ascending(a => a.StartTime)
                : Builders<Auction>.Sort.Descending(a => a.StartTime)
        };

        // Pagination
        var skip = (page - 1) * pageSize;
        var items = await _auctions
            .Find(combinedFilter)
            .Sort(sortDefinition)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<Auction>> SearchSimilarAuctionsAsync(float[] queryVector, int limit = 5)
    {
        var pipeline = new MongoDB.Bson.BsonDocument[]
        {
            new MongoDB.Bson.BsonDocument("$vectorSearch", new MongoDB.Bson.BsonDocument
            {
                { "index", "vector_index" },
                { "path", "ImageVector" },
                { "queryVector", new MongoDB.Bson.BsonArray(queryVector) },
                { "numCandidates", 100 },
                { "limit", limit }
            })
        };

        var result = await _auctions.Aggregate<Auction>(pipeline).ToListAsync();
        return result;
    }
}
