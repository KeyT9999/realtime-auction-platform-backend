using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services
{
    public interface IAuctionService
    {
        Task<Auction> CreateAuctionAsync(Auction auction);
        Task<List<Auction>> GetActiveAuctionsAsync();
        Task<Auction?> GetAuctionByIdAsync(string id);
        Task PlaceBidAsync(string auctionId, string userId, decimal amount);
    }
}
