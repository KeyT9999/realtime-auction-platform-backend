using RealtimeAuction.Api.Dtos.Bid;
using RealtimeAuction.Api.Models;

namespace RealtimeAuction.Api.Services;

public interface IBidService
{
    /// <summary>
    /// Đặt giá với cơ chế hold balance
    /// </summary>
    Task<BidResult> PlaceBidAsync(string userId, CreateBidDto request);
    
    /// <summary>
    /// Release hold khi user bị outbid
    /// </summary>
    Task ReleaseHoldAsync(string userId, string auctionId);
    
    /// <summary>
    /// Release hold cho tất cả bidders khi auction kết thúc (trừ winner)
    /// </summary>
    Task ReleaseAllHoldsExceptWinnerAsync(string auctionId, string winnerId);
    
    /// <summary>
    /// Chuyển hold của winner thành payment cho seller
    /// </summary>
    Task ProcessWinnerPaymentAsync(string auctionId, string winnerId, string sellerId);
    
    /// <summary>
    /// Release tất cả holds trong auction (dùng khi cancel auction)
    /// </summary>
    Task ReleaseAllHoldsAsync(string auctionId);
    
    /// <summary>
    /// Đảm bảo user có hold đủ amount cho auction
    /// </summary>
    Task<bool> EnsureHoldAsync(string userId, string auctionId, decimal amount);
    
    /// <summary>
    /// Confirm transaction (buyer hoặc seller)
    /// </summary>
    Task<bool> ConfirmTransactionAsync(string auctionId, string userId, bool isBuyer);
    
    /// <summary>
    /// Cancel transaction và refund winner
    /// </summary>
    Task<bool> CancelTransactionAsync(string auctionId, string userId);
}

public class BidResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Bid? Bid { get; set; }
    public string? OutbidUserId { get; set; }
}
