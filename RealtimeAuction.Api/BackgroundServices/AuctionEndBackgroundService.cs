using Microsoft.AspNetCore.SignalR;
using RealtimeAuction.Api.Hubs;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.BackgroundServices;

public class AuctionEndBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuctionEndBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public AuctionEndBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AuctionEndBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuctionEndBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEndedAuctionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuctionEndBackgroundService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("AuctionEndBackgroundService stopped");
    }

    private async Task ProcessEndedAuctionsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        var auctionRepository = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var bidRepository = scope.ServiceProvider.GetRequiredService<IBidRepository>();
        var bidService = scope.ServiceProvider.GetRequiredService<IBidService>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<AuctionHub>>();

        // Lấy các auctions đã hết hạn nhưng vẫn đang Active
        var auctions = await auctionRepository.GetAllAsync();
        var endedAuctions = auctions
            .Where(a => a.Status == AuctionStatus.Active && a.EndTime <= DateTime.UtcNow)
            .ToList();

        _logger.LogInformation("Found {Count} auctions to complete", endedAuctions.Count);

        foreach (var auction in endedAuctions)
        {
            try
            {
                await ProcessAuctionEndAsync(
                    auction.Id!, 
                    auction.SellerId,
                    auctionRepository, 
                    bidRepository, 
                    bidService,
                    hubContext);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing auction {AuctionId}", auction.Id);
            }
        }
    }

    private async Task ProcessAuctionEndAsync(
        string auctionId,
        string sellerId,
        IAuctionRepository auctionRepository,
        IBidRepository bidRepository,
        IBidService bidService,
        IHubContext<AuctionHub> hubContext)
    {
        using var scope = _serviceProvider.CreateScope();
        var transactionRepository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        
        _logger.LogInformation("Processing auction end: {AuctionId}", auctionId);

        // 1. Lấy highest bid
        var highestBid = await bidRepository.GetHighestBidAsync(auctionId);
        
        if (highestBid == null)
        {
            // Không có bid nào - auction không thành công
            var auction = await auctionRepository.GetByIdAsync(auctionId);
            if (auction != null)
            {
                auction.Status = AuctionStatus.Failed;
                auction.UpdatedAt = DateTime.UtcNow;
                await auctionRepository.UpdateAsync(auction);

                // Notify seller
                await AuctionHub.NotifyAuctionEnded(hubContext, auctionId, new
                {
                    AuctionId = auctionId,
                    Status = "Failed",
                    Message = "Phiên đấu giá không có người tham gia"
                });

                _logger.LogInformation("Auction {AuctionId} ended with no bids", auctionId);
            }
            return;
        }

        // 2. Có winner - cập nhật auction status
        var auctionToComplete = await auctionRepository.GetByIdAsync(auctionId);
        if (auctionToComplete != null)
        {
            auctionToComplete.Status = AuctionStatus.Completed;
            auctionToComplete.WinnerId = highestBid.UserId;
            auctionToComplete.FinalPrice = highestBid.Amount;
            auctionToComplete.UpdatedAt = DateTime.UtcNow;
            await auctionRepository.UpdateAsync(auctionToComplete);
        }

        // 3. Mark winning bid
        highestBid.IsWinningBid = true;
        await bidRepository.UpdateAsync(highestBid);

        // 4. Release hold cho tất cả losers
        await bidService.ReleaseAllHoldsExceptWinnerAsync(auctionId, highestBid.UserId);

        // 5. Tạo Transaction Pending (giữ hold của winner, chưa chuyển tiền)
        var winner = await userRepository.GetByIdAsync(highestBid.UserId);
        if (winner != null)
        {
            var pendingTransaction = new Transaction
            {
                UserId = highestBid.UserId,
                Type = TransactionType.Payment,
                Amount = -highestBid.HeldAmount,
                Description = $"Thanh toán đấu giá - chờ xác nhận",
                RelatedAuctionId = auctionId,
                RelatedBidId = highestBid.Id,
                Status = TransactionStatus.Pending,
                BuyerConfirmed = false,
                SellerConfirmed = false,
                BalanceBefore = winner.EscrowBalance,
                BalanceAfter = winner.EscrowBalance, // Giữ nguyên vì chưa chuyển
                CreatedAt = DateTime.UtcNow
            };
            await transactionRepository.CreateAsync(pendingTransaction);
        }

        // 6. Notify winner và seller
        await AuctionHub.NotifyAuctionEnded(hubContext, auctionId, new
        {
            AuctionId = auctionId,
            Status = "Completed",
            WinnerId = highestBid.UserId,
            FinalPrice = highestBid.Amount,
            Message = "Đấu giá kết thúc thành công"
        });

        await AuctionHub.NotifyUserWon(hubContext, highestBid.UserId, new
        {
            AuctionId = auctionId,
            AuctionTitle = auctionToComplete?.Title,
            WinningBid = highestBid.Amount,
            Message = "Chúc mừng! Bạn đã thắng đấu giá"
        });

        _logger.LogInformation(
            "Auction {AuctionId} completed. Winner: {WinnerId}, Price: {Price}",
            auctionId, highestBid.UserId, highestBid.Amount);
    }
}
