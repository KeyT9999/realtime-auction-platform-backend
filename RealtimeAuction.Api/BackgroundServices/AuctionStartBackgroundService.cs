using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using RealtimeAuction.Api.Hubs;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.BackgroundServices;

/// <summary>
/// Ensures scheduled auctions (Status = Pending) automatically move to Active when StartTime is reached.
/// </summary>
public sealed class AuctionStartBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuctionStartBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    public AuctionStartBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AuctionStartBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuctionStartBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledAuctionsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in AuctionStartBackgroundService");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // graceful shutdown
            }
        }

        _logger.LogInformation("AuctionStartBackgroundService stopped");
    }

    private async Task ProcessScheduledAuctionsAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var hub = scope.ServiceProvider.GetRequiredService<IHubContext<AuctionHub>>();

        var auctions = db.GetCollection<Auction>("Auctions");
        var now = DateTime.UtcNow;

        // Find auctions that were approved but scheduled (Pending) and should start now.
        var filter = Builders<Auction>.Filter.And(
            Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Pending),
            Builders<Auction>.Filter.Lte(a => a.StartTime, now),
            Builders<Auction>.Filter.Gt(a => a.EndTime, now)
        );

        var candidates = await auctions
            .Find(filter)
            .Project(a => new { a.Id, a.Title, a.SellerId })
            .ToListAsync(stoppingToken);

        if (candidates.Count == 0) return;

        _logger.LogInformation("Found {Count} scheduled auctions to activate", candidates.Count);

        foreach (var c in candidates)
        {
            if (string.IsNullOrWhiteSpace(c.Id)) continue;

            // Atomic guard: only flip if still Pending.
            var updateResult = await auctions.UpdateOneAsync(
                Builders<Auction>.Filter.And(
                    Builders<Auction>.Filter.Eq(a => a.Id, c.Id),
                    Builders<Auction>.Filter.Eq(a => a.Status, AuctionStatus.Pending)
                ),
                Builders<Auction>.Update
                    .Set(a => a.Status, AuctionStatus.Active)
                    .Set(a => a.UpdatedAt, now),
                cancellationToken: stoppingToken);

            if (updateResult.ModifiedCount == 0) continue;

            // Realtime: notify auction room + seller + admins (keep payload shape similar to AuctionController).
            await hub.Clients.Group(GroupNames.Auction(c.Id)).SendAsync("AuctionStatusChanged", new
            {
                auctionId = c.Id,
                status = (int)AuctionStatus.Active,
                message = "Trạng thái đấu giá thay đổi: Active"
            }, stoppingToken);

            if (!string.IsNullOrWhiteSpace(c.SellerId))
            {
                await hub.Clients.Group(GroupNames.User(c.SellerId)).SendAsync("UserNotification", new
                {
                    type = "AuctionStatusChanged",
                    auctionId = c.Id,
                    status = (int)AuctionStatus.Active,
                    message = $"Trạng thái đấu giá \"{c.Title}\" -> Active"
                }, stoppingToken);
            }

            await hub.Clients.Group(GroupNames.Admins).SendAsync("AdminNotification", new
            {
                type = "AuctionStatusChanged",
                auctionId = c.Id,
                status = (int)AuctionStatus.Active,
                message = $"[Auction] Status Active for {c.Title}"
            }, stoppingToken);
        }
    }
}

