using Quartz;
using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using Microsoft.AspNetCore.SignalR;
using RealtimeAuction.Api.Hubs;

namespace RealtimeAuction.Api.Jobs
{
    [DisallowConcurrentExecution]
    public class AuctionStatusJob : IJob
    {
        private readonly IMongoCollection<Auction> _auctions;
        private readonly IMongoCollection<Order> _orders;
        private readonly IHubContext<AuctionHub> _hubContext;

        public AuctionStatusJob(IMongoDatabase database, IHubContext<AuctionHub> hubContext)
        {
            _auctions = database.GetCollection<Auction>("Auctions");
            _orders = database.GetCollection<Order>("Orders");
            _hubContext = hubContext;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            // 1. Find auctions that should move from Scheduled to Active
            var now = DateTime.UtcNow;
            var scheduledToActiveFilter = Builders<Auction>.Filter.And(
                Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Scheduled),
                Builders<Auction>.Filter.Lte(a => a.StartTime, now)
            );
            var activeUpdate = Builders<Auction>.Update.Set(a => a.Status, AuctionStatus.Active);
            await _auctions.UpdateManyAsync(scheduledToActiveFilter, activeUpdate);

            // 2. Find auctions that should move from Active to Ended
            var activeToEndedFilter = Builders<Auction>.Filter.And(
                Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Active),
                Builders<Auction>.Filter.Lte(a => a.EndTime, now)
            );

            // We need to process these one by one to create orders
            var endingAuctions = await _auctions.Find(activeToEndedFilter).ToListAsync();

            foreach (var auction in endingAuctions)
            {
                var newStatus = auction.WinnerId != null ? AuctionStatus.Ended : AuctionStatus.Expired;
                
                var update = Builders<Auction>.Update.Set(a => a.Status, newStatus);
                await _auctions.UpdateOneAsync(a => a.Id == auction.Id, update);

                if (newStatus == AuctionStatus.Ended && auction.WinnerId != null)
                {
                    // Create Order (Escrow)
                    var order = new Order
                    {
                        AuctionId = auction.Id!,
                        SellerId = auction.SellerId,
                        BuyerId = auction.WinnerId,
                        Amount = auction.CurrentPrice,
                        EscrowStatus = EscrowStatus.MoneyHeld
                    };
                    await _orders.InsertOneAsync(order);

                    // Notify Winner/Seller (Simulated via SignalR for now)
                    // In real app, send Push Notification / Email here
                }
                
                await _hubContext.Clients.Group(auction.Id!).SendAsync("AuctionEnded", new 
                { 
                    AuctionId = auction.Id, 
                    Status = newStatus,
                    WinnerId = auction.WinnerId 
                });
            }
        }
    }
}
