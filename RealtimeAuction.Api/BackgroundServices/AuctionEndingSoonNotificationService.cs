using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using RealtimeAuction.Api.Hubs;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;
using RealtimeAuction.Api.Repositories;
using RealtimeAuction.Api.Services;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.BackgroundServices;

/// <summary>
/// Ending soon: Realtime = SignalR to everyone currently viewing the auction page.
/// Offline = email to watchlist users who are not viewing (if SendEmailWhenOffline is enabled).
/// Runs every 15 minutes to check for auctions ending within the next hour.
/// </summary>
public class AuctionEndingSoonNotificationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<AuctionHub> _hubContext;
    private readonly NotificationSettings _notificationSettings;
    private readonly ILogger<AuctionEndingSoonNotificationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _endingSoonThreshold = TimeSpan.FromHours(1);

    public AuctionEndingSoonNotificationService(
        IServiceProvider serviceProvider,
        IHubContext<AuctionHub> hubContext,
        IOptions<NotificationSettings> notificationSettings,
        ILogger<AuctionEndingSoonNotificationService> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _notificationSettings = notificationSettings?.Value ?? new NotificationSettings();
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
                // 1) Realtime: notify everyone currently viewing this auction page (SignalR only)
                var timeRemaining = auction.EndTime - DateTime.UtcNow;
                var timeRemainingStr = FormatTimeRemaining(timeRemaining);
                var currentPriceStr = FormatCurrency(auction.CurrentPrice);
                var auctionUrl = $"http://localhost:5173/auctions/{auction.Id}";
                await AuctionHub.NotifyEndingSoon(_hubContext, auction.Id!, new
                {
                    AuctionId = auction.Id,
                    AuctionTitle = auction.Title,
                    EndTime = auction.EndTime,
                    TimeRemaining = timeRemainingStr,
                    CurrentPrice = currentPriceStr,
                    AuctionUrl = auctionUrl
                });

                // 2) Offline: send email to watchlist users who are NOT currently viewing (if email enabled)
                await SendEndingSoonEmailsForOfflineWatchersAsync(
                    auction,
                    watchlistRepository,
                    userRepository,
                    emailService);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending ending soon for auction {AuctionId}", auction.Id);
            }
        }
    }

    /// <summary>
    /// Send ending-soon email only to watchlist users who are offline (not currently viewing the auction).
    /// Realtime notification already sent via SignalR to all viewers.
    /// </summary>
    private async Task SendEndingSoonEmailsForOfflineWatchersAsync(
        Auction auction,
        IWatchlistRepository watchlistRepository,
        IUserRepository userRepository,
        IEmailService emailService)
    {
        if (!_notificationSettings.SendEmailWhenOffline)
            return;

        var watchlists = await watchlistRepository.GetByAuctionIdAsync(auction.Id!);
        var unsent = watchlists.Where(w => !w.EndingSoonEmailSent).ToList();
        if (!unsent.Any()) return;

        // Users currently viewing this auction already got SignalR; only email those offline
        var viewerUserIds = new HashSet<string>(AuctionHub.GetViewerUserIds(auction.Id!), StringComparer.Ordinal);

        var timeRemaining = auction.EndTime - DateTime.UtcNow;
        var timeRemainingStr = FormatTimeRemaining(timeRemaining);
        var currentPriceStr = FormatCurrency(auction.CurrentPrice);
        var auctionUrl = $"http://localhost:5173/auctions/{auction.Id}";

        foreach (var watchlist in unsent)
        {
            if (viewerUserIds.Contains(watchlist.UserId))
                continue; // already notified via SignalR

            try
            {
                var user = await userRepository.GetByIdAsync(watchlist.UserId);
                if (user == null || string.IsNullOrEmpty(user.Email))
                    continue;

                await emailService.SendAuctionEndingSoonEmailAsync(
                    user.Email,
                    user.FullName,
                    auction.Title,
                    timeRemainingStr,
                    currentPriceStr,
                    auctionUrl);

                watchlist.EndingSoonEmailSent = true;
                watchlist.EndingSoonEmailSentAt = DateTime.UtcNow;
                await watchlistRepository.UpdateAsync(watchlist);

                _logger.LogInformation("Sent ending soon email to {Email} for auction {AuctionId} (user offline)",
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
