using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace RealtimeAuction.Api.Hubs;

public class AuctionHub : Hub
{
    // Track viewers in each auction room: AuctionId -> HashSet<ConnectionId>
    private static readonly ConcurrentDictionary<string, HashSet<string>> _auctionViewers = new();
    
    // Track user connections: ConnectionId -> UserId
    private static readonly ConcurrentDictionary<string, string?> _userConnections = new();
    
    // Track which auction each connection is viewing: ConnectionId -> AuctionId
    private static readonly ConcurrentDictionary<string, string> _connectionAuctions = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.Identity?.Name;
        _userConnections.TryAdd(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Remove from user connections
        _userConnections.TryRemove(Context.ConnectionId, out _);

        // Remove from auction room if they were viewing one
        if (_connectionAuctions.TryRemove(Context.ConnectionId, out var auctionId))
        {
            await LeaveAuctionGroup(auctionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinAuctionGroup(string auctionId)
    {
        if (string.IsNullOrWhiteSpace(auctionId))
            return;

        // Leave previous auction if any
        if (_connectionAuctions.TryGetValue(Context.ConnectionId, out var previousAuctionId))
        {
            await LeaveAuctionGroup(previousAuctionId);
        }

        // Join the new auction group
        await Groups.AddToGroupAsync(Context.ConnectionId, auctionId);
        
        // Track this viewer
        _auctionViewers.AddOrUpdate(
            auctionId,
            new HashSet<string> { Context.ConnectionId },
            (key, existing) =>
            {
                existing.Add(Context.ConnectionId);
                return existing;
            });

        _connectionAuctions[Context.ConnectionId] = auctionId;

        // Broadcast updated viewer count
        var viewerCount = _auctionViewers.TryGetValue(auctionId, out var viewers) ? viewers.Count : 0;
        await Clients.Group(auctionId).SendAsync("ViewerCountUpdated", new
        {
            AuctionId = auctionId,
            ViewerCount = viewerCount
        });
    }

    public async Task LeaveAuctionGroup(string auctionId)
    {
        if (string.IsNullOrWhiteSpace(auctionId))
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, auctionId);

        // Remove from viewers tracking
        if (_auctionViewers.TryGetValue(auctionId, out var viewers))
        {
            viewers.Remove(Context.ConnectionId);
            
            if (viewers.Count == 0)
            {
                _auctionViewers.TryRemove(auctionId, out _);
            }
            else
            {
                // Broadcast updated viewer count
                await Clients.Group(auctionId).SendAsync("ViewerCountUpdated", new
                {
                    AuctionId = auctionId,
                    ViewerCount = viewers.Count
                });
            }
        }

        _connectionAuctions.TryRemove(Context.ConnectionId, out _);
    }

    // Called from BidController when a new bid is placed
    public static async Task NotifyNewBid(IHubContext<AuctionHub> hubContext, string auctionId, object bidData)
    {
        await hubContext.Clients.Group(auctionId).SendAsync("UpdateBid", bidData);
    }

    // Notify specific user that they've been outbid
    public static async Task NotifyUserOutbid(IHubContext<AuctionHub> hubContext, string userId, object outbidData)
    {
        // Find all connections for this user
        var userConnectionIds = _userConnections
            .Where(kvp => kvp.Value == userId)
            .Select(kvp => kvp.Key)
            .ToList();

        if (userConnectionIds.Any())
        {
            await hubContext.Clients.Clients(userConnectionIds).SendAsync("UserOutbid", outbidData);
        }
    }

    // Notify when auction ends
    public static async Task NotifyAuctionEnded(IHubContext<AuctionHub> hubContext, string auctionId, object endData)
    {
        await hubContext.Clients.Group(auctionId).SendAsync("AuctionEnded", endData);
    }

    // Notify when auction time is extended
    public static async Task NotifyTimeExtended(IHubContext<AuctionHub> hubContext, string auctionId, object extendData)
    {
        await hubContext.Clients.Group(auctionId).SendAsync("TimeExtended", extendData);
    }

    // Notify when seller accepts current bid
    public static async Task NotifyAuctionAccepted(IHubContext<AuctionHub> hubContext, string auctionId, object acceptData)
    {
        await hubContext.Clients.Group(auctionId).SendAsync("AuctionAccepted", acceptData);
    }

    // Notify when someone buyouts the auction
    public static async Task NotifyAuctionBuyout(IHubContext<AuctionHub> hubContext, string auctionId, object buyoutData)
    {
        await hubContext.Clients.Group(auctionId).SendAsync("AuctionBuyout", buyoutData);
    }

    // Notify when auction is cancelled
    public static async Task NotifyAuctionCancelled(IHubContext<AuctionHub> hubContext, string auctionId, object cancelData)
    {
        await hubContext.Clients.Group(auctionId).SendAsync("AuctionCancelled", cancelData);
    }

    // Notify user that they won the auction
    public static async Task NotifyUserWon(IHubContext<AuctionHub> hubContext, string userId, object winData)
    {
        var userConnectionIds = _userConnections
            .Where(kvp => kvp.Value == userId)
            .Select(kvp => kvp.Key)
            .ToList();

        if (userConnectionIds.Any())
        {
            await hubContext.Clients.Clients(userConnectionIds).SendAsync("UserWon", winData);
        }
    }

    // Notify user that their balance hold was released (outbid)
    public static async Task NotifyBalanceReleased(IHubContext<AuctionHub> hubContext, string userId, object releaseData)
    {
        var userConnectionIds = _userConnections
            .Where(kvp => kvp.Value == userId)
            .Select(kvp => kvp.Key)
            .ToList();

        if (userConnectionIds.Any())
        {
            await hubContext.Clients.Clients(userConnectionIds).SendAsync("BalanceReleased", releaseData);
        }
    }

    // Get current viewer count for an auction
    public int GetViewerCount(string auctionId)
    {
        return _auctionViewers.TryGetValue(auctionId, out var viewers) ? viewers.Count : 0;
    }
}
