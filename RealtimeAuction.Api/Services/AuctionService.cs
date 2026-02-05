using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using RealtimeAuction.Api.Hubs;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Repositories;

namespace RealtimeAuction.Api.Services
{
    public class AuctionService : IAuctionService
    {
        private readonly IMongoCollection<Auction> _auctions;
        private readonly IMongoCollection<Bid> _bids;
        private readonly IMongoCollection<User> _users;
        private readonly IHubContext<AuctionHub> _hubContext;
        private readonly MongoClient _mongoClient;

        public AuctionService(IMongoDatabase database, IHubContext<AuctionHub> hubContext, IMongoClient mongoClient)
        {
            _auctions = database.GetCollection<Auction>("Auctions");
            _bids = database.GetCollection<Bid>("Bids");
            _users = database.GetCollection<User>("Users");
            _hubContext = hubContext;
            // We need to cast back to MongoClient because IMongoDatabase doesn't expose Client directly in standard DI usually, 
            // but we can inject IMongoClient directly if registered.
             // Ideally we registered IMongoClient or Singleton<IMongoDatabase>
             // In Program.cs we saw IMongoDatabase registered via factory returning client.GetDatabase.
             // We need the client to start a session.
             // We will assume IMongoClient is registered or we can get it.
             // Actually, Program.cs only registered IMongoDatabase. This is a small issue.
             // I will fix Program.cs to register IMongoClient as well later.
            _mongoClient = (MongoClient)mongoClient; 
        }

        public async Task<Auction> CreateAuctionAsync(Auction auction)
        {
            await _auctions.InsertOneAsync(auction);
            return auction;
        }

        public async Task<List<Auction>> GetActiveAuctionsAsync()
        {
            return await _auctions.Find(a => a.Status == AuctionStatus.Active).ToListAsync();
        }

        public async Task<Auction?> GetAuctionByIdAsync(string id)
        {
            return await _auctions.Find(a => a.Id == id).FirstOrDefaultAsync();
        }

        public async Task PlaceBidAsync(string auctionId, string userId, decimal amount)
        {
            using var session = await _mongoClient.StartSessionAsync();
            session.StartTransaction();

            try
            {
                var auction = await _auctions.Find(session, a => a.Id == auctionId).FirstOrDefaultAsync();
                if (auction == null) throw new Exception("Auction not found");
                if (auction.Status != AuctionStatus.Active) throw new Exception("Auction is not active");
                if (DateTime.UtcNow < auction.StartTime || DateTime.UtcNow > auction.EndTime) throw new Exception("Auction time is invalid");
                
                // Allow equal bid? Usually must be higher. And usually by step.
                // Requirement: Amount >= CurrentPrice + StepPrice. 
                // Edge case: first bid. If CurrentPrice is StartPrice and no winner?
                // Logic: If WinnerId is null, Amount >= StartPrice.
                // If WinnerId is not null, Amount >= CurrentPrice + StepPrice.
                
                decimal minBid = (auction.WinnerId == null) ? auction.StartPrice : auction.CurrentPrice + auction.StepPrice;
                if (amount < minBid) throw new Exception($"Bid amount must be at least {minBid}");

                var user = await _users.Find(session, u => u.Id == userId).FirstOrDefaultAsync();
                if (user == null) throw new Exception("User not found");
                if (user.AvailableBalance < amount) throw new Exception("Insufficient balance");

                // 1. Lock Money (Current User)
                var userFilter = Builders<User>.Filter.Eq(u => u.Id, userId);
                var userUpdate = Builders<User>.Update
                    .Inc(u => u.AvailableBalance, -amount)
                    .Inc(u => u.EscrowBalance, amount);
                
                await _users.UpdateOneAsync(session, userFilter, userUpdate);

                // 2. Refund Money (Previous Bidder)
                if (auction.WinnerId != null)
                {
                    // Find previous winner
                    var prevWinnerId = auction.WinnerId;
                    var prevPrice = auction.CurrentPrice;

                    var prevWinnerFilter = Builders<User>.Filter.Eq(u => u.Id, prevWinnerId);
                    var prevWinnerUpdate = Builders<User>.Update
                        .Inc(u => u.AvailableBalance, prevPrice)
                        .Inc(u => u.EscrowBalance, -prevPrice);

                    await _users.UpdateOneAsync(session, prevWinnerFilter, prevWinnerUpdate);
                }

                // 3. Update Auction
                // Sniper Protection
                var newEndTime = auction.EndTime;
                bool timeExtended = false;
                if ((auction.EndTime - DateTime.UtcNow).TotalSeconds < 30)
                {
                    newEndTime = auction.EndTime.AddMinutes(2);
                    timeExtended = true;
                }

                var auctionFilter = Builders<Auction>.Filter.Eq(a => a.Id, auctionId);
                var auctionUpdate = Builders<Auction>.Update
                    .Set(a => a.CurrentPrice, amount)
                    .Set(a => a.WinnerId, userId)
                    .Set(a => a.EndTime, newEndTime);

                await _auctions.UpdateOneAsync(session, auctionFilter, auctionUpdate);

                // 4. Create Bid Record
                var bid = new Bid
                {
                    AuctionId = auctionId,
                    UserId = userId,
                    Amount = amount,
                    Timestamp = DateTime.UtcNow
                };
                await _bids.InsertOneAsync(session, bid);

                await session.CommitTransactionAsync();

                // 5. Broadcast (Outside Transaction / After Commit)
                await _hubContext.Clients.Group(auctionId).SendAsync("UpdateBid", new 
                { 
                    Type = "BID_UPDATE",
                    CurrentPrice = amount, 
                    WinnerId = userId[..4] + "***", // Masked
                    EndTime = newEndTime 
                });

                if (timeExtended)
                {
                    await _hubContext.Clients.Group(auctionId).SendAsync("TimeExtended", new 
                    { 
                        Type = "TIME_EXTENDED",
                        NewEndTime = newEndTime 
                    });
                }
            }
            catch (Exception)
            {
                await session.AbortTransactionAsync();
                throw;
            }
        }
    }
}
