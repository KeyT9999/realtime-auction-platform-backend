using Microsoft.AspNetCore.SignalR;
using RealtimeAuction.Api.Hubs;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Observability;
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

            try
        {
            await Task.Delay(_checkInterval, stoppingToken);
        }
        catch (TaskCanceledException)
        {
            // Graceful exit when application is stopping
        }
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
                    auction,
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
        Auction auctionSnapshot,
        IAuctionRepository auctionRepository,
        IBidRepository bidRepository,
        IBidService bidService,
        IHubContext<AuctionHub> hubContext)
    {
        using var scope = _serviceProvider.CreateScope();
        var transactionRepository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var processedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "{EventName}: Processing auction end. AuctionId {AuctionId}, SellerId {SellerId}, CurrentPrice {CurrentPrice}, BidCount {BidCount}, AuctionEndTimeUtc {AuctionEndTimeUtc}, AuctionEndTimeUtcMs {AuctionEndTimeUtcMs}, ServerTimeUtc {ServerTimeUtc}, ServerTimeUtcMs {ServerTimeUtcMs}",
            BusinessEvents.AuctionEndProcessingStarted,
            auctionSnapshot.Id,
            auctionSnapshot.SellerId,
            auctionSnapshot.CurrentPrice,
            auctionSnapshot.BidCount,
            auctionSnapshot.EndTime,
            AuditLog.ToUnixTimeMilliseconds(auctionSnapshot.EndTime),
            processedAt,
            AuditLog.ToUnixTimeMilliseconds(processedAt));

        // 1. Lấy highest bid
        var highestBid = await bidRepository.GetHighestBidAsync(auctionSnapshot.Id!);
        
        if (highestBid == null)
        {
            // Không có bid nào - auction không thành công
            var auction = await auctionRepository.GetByIdAsync(auctionSnapshot.Id!);
            if (auction != null)
            {
                auction.Status = AuctionStatus.Failed;
                auction.UpdatedAt = DateTime.UtcNow;
                await auctionRepository.UpdateAsync(auction);

                // Notify seller
                await AuctionHub.NotifyAuctionEnded(hubContext, auctionSnapshot.Id!, new
                {
                    AuctionId = auctionSnapshot.Id,
                    Status = "Failed",
                    Message = "Phiên đấu giá không có người tham gia"
                });

                var endedAt = DateTime.UtcNow;
                _logger.LogInformation(
                    "{EventName}: Auction ended without bids. AuctionId {AuctionId}, SellerId {SellerId}, AuctionEndTimeUtc {AuctionEndTimeUtc}, AuctionEndTimeUtcMs {AuctionEndTimeUtcMs}, ServerTimeUtc {ServerTimeUtc}, ServerTimeUtcMs {ServerTimeUtcMs}",
                    BusinessEvents.AuctionClosedNoBids,
                    auction.Id,
                    auction.SellerId,
                    auction.EndTime,
                    AuditLog.ToUnixTimeMilliseconds(auction.EndTime),
                    endedAt,
                    AuditLog.ToUnixTimeMilliseconds(endedAt));
            }
            return;
        }

        // 2. Có winner - cập nhật auction status
        var auctionToComplete = await auctionRepository.GetByIdAsync(auctionSnapshot.Id!);
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
        await bidService.ReleaseAllHoldsExceptWinnerAsync(auctionSnapshot.Id!, highestBid.UserId);

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
                RelatedAuctionId = auctionSnapshot.Id,
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

        // 5b. Tạo Order cho giao dịch
        var orderService = scope.ServiceProvider.GetRequiredService<IOrderService>();
        var productImage = auctionToComplete?.Images?.FirstOrDefault();
        await orderService.CreateOrderFromAuctionAsync(
            auctionSnapshot.Id!,
            highestBid.UserId,
            auctionSnapshot.SellerId,
            highestBid.Amount,
            auctionToComplete?.Title ?? "Đấu giá",
            productImage
        );

        // 6. Notify winner và seller
        await AuctionHub.NotifyAuctionEnded(hubContext, auctionSnapshot.Id!, new
        {
            AuctionId = auctionSnapshot.Id,
            Status = "Completed",
            WinnerId = highestBid.UserId,
            FinalPrice = highestBid.Amount,
            Message = "Đấu giá kết thúc thành công"
        });

        await AuctionHub.NotifyUserWon(hubContext, highestBid.UserId, new
        {
            AuctionId = auctionSnapshot.Id,
            AuctionTitle = auctionToComplete?.Title,
            WinningBid = highestBid.Amount,
            Message = "Chúc mừng! Bạn đã thắng đấu giá"
        });

        // 7. Send Auction Won email to winner
        if (winner != null && !string.IsNullOrEmpty(winner.Email))
        {
            try
            {
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
                var transactionUrl = $"{configuration["FrontendUrl"]}/auctions/{auctionSnapshot.Id}";
                var winningBidStr = FormatCurrency(highestBid.Amount);
                
                await emailService.SendAuctionWonEmailAsync(
                    winner.Email,
                    winner.FullName,
                    auctionToComplete?.Title ?? "Đấu giá",
                    winningBidStr,
                    transactionUrl);
                
                _logger.LogInformation("Sent auction won email to {Email}", winner.Email);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send auction won email to {Email}", winner.Email);
            }
        }

        _logger.LogInformation(
            "{EventName}: Auction completed with winner selected. AuctionId {AuctionId}, SellerId {SellerId}, WinnerId {WinnerId}, WinningBidId {WinningBidId}, FinalPrice {FinalPrice}, HeldAmount {HeldAmount}, AuctionEndTimeUtc {AuctionEndTimeUtc}, AuctionEndTimeUtcMs {AuctionEndTimeUtcMs}, ServerTimeUtc {ServerTimeUtc}, ServerTimeUtcMs {ServerTimeUtcMs}",
            BusinessEvents.AuctionClosedWinnerSelected,
            auctionSnapshot.Id,
            auctionSnapshot.SellerId,
            highestBid.UserId,
            highestBid.Id,
            highestBid.Amount,
            highestBid.HeldAmount,
            auctionSnapshot.EndTime,
            AuditLog.ToUnixTimeMilliseconds(auctionSnapshot.EndTime),
            DateTime.UtcNow,
            AuditLog.ToUnixTimeMilliseconds(DateTime.UtcNow));
    }

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} ₫", amount);
    }
}
