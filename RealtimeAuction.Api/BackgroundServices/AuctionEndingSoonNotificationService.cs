using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;

namespace RealtimeAuction.Api.BackgroundServices;

/// <summary>
/// Background service that sends email notifications to watchers when auctions are ending soon (< 1 hour).
/// Runs every 15 minutes to check for auctions ending within the next hour.
/// </summary>
public class AuctionEndingSoonNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuctionEndingSoonNotificationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _endingSoonThreshold = TimeSpan.FromHours(1);

    public AuctionEndingSoonNotificationService(
        IServiceProvider serviceProvider,
        ILogger<AuctionEndingSoonNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuctionEndingSoonNotificationService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEndingSoonNotificationsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuctionEndingSoonNotificationService");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("AuctionEndingSoonNotificationService stopped");
    }

    private async Task ProcessEndingSoonNotificationsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        
        var auctionRepository = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var watchlistRepository = scope.ServiceProvider.GetRequiredService<IWatchlistRepository>();
        var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var now = DateTime.UtcNow;
        var endingSoonTime = now.Add(_endingSoonThreshold);

        // Get active auctions ending within the threshold
        var auctions = await auctionRepository.GetAllAsync();
        var endingSoonAuctions = auctions
            .Where(a => a.Status == AuctionStatus.Active 
                        && a.EndTime > now 
                        && a.EndTime <= endingSoonTime)
            .ToList();

        _logger.LogInformation("Found {Count} auctions ending soon", endingSoonAuctions.Count);

        foreach (var auction in endingSoonAuctions)
        {
            try
            {
                await SendEndingSoonEmailsForAuctionAsync(
                    auction,
                    watchlistRepository,
                    userRepository,
                    emailService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ending soon emails for auction {AuctionId}", auction.Id);
            }
        }
    }

    private async Task SendEndingSoonEmailsForAuctionAsync(
        Auction auction,
        IWatchlistRepository watchlistRepository,
        IUserRepository userRepository,
        IEmailService emailService)
    {
        // Get all watchers for this auction
        var watchlists = await watchlistRepository.GetByAuctionIdAsync(auction.Id!);
        var unsent = watchlists.Where(w => !w.EndingSoonEmailSent).ToList();

        if (!unsent.Any())
        {
            return;
        }

        _logger.LogInformation("Sending ending soon emails to {Count} watchers for auction {AuctionId}", 
            unsent.Count, auction.Id);

        var timeRemaining = auction.EndTime - DateTime.UtcNow;
        var timeRemainingStr = FormatTimeRemaining(timeRemaining);
        var currentPriceStr = FormatCurrency(auction.CurrentPrice);
        var auctionUrl = $"http://localhost:5173/auctions/{auction.Id}";

        foreach (var watchlist in unsent)
        {
            try
            {
                var user = await userRepository.GetByIdAsync(watchlist.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email))
                {
                    continue;
                }

                // Send email
                await emailService.SendAuctionEndingSoonEmailAsync(
                    user.Email,
                    user.FullName,
                    auction.Title,
                    timeRemainingStr,
                    currentPriceStr,
                    auctionUrl);

                // Mark as sent - need to update the watchlist
                watchlist.EndingSoonEmailSent = true;
                watchlist.EndingSoonEmailSentAt = DateTime.UtcNow;
                
                // Note: We need an update method in the repository
                // For now, we'll just log that we sent the email
                _logger.LogInformation("Sent ending soon email to {Email} for auction {AuctionId}", 
                    user.Email, auction.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ending soon email to user {UserId}", watchlist.UserId);
            }
        }
    }

    private static string FormatTimeRemaining(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMinutes < 60)
        {
            return $"{(int)timeSpan.TotalMinutes} phút";
        }
        return $"{(int)timeSpan.TotalHours} giờ {timeSpan.Minutes} phút";
    }

    private static string FormatCurrency(decimal amount)
    {
        return string.Format(new System.Globalization.CultureInfo("vi-VN"), "{0:N0} ₫", amount);
    }
}
