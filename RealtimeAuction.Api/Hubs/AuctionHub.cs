using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;

namespace RealtimeAuction.Api.Hubs;

public class AuctionHub : Hub
{
    // Track viewers in each auction room: AuctionId -> HashSet<ConnectionId>
    private static readonly ConcurrentDictionary<string, HashSet<string>> _auctionViewers = new();
    
    // Track user connections: ConnectionId -> UserId (NameIdentifier for NotifyUserOutbid)
    private static readonly ConcurrentDictionary<string, string?> _userConnections = new();
    
    // Track which auction each connection is viewing: ConnectionId -> AuctionId
    private static readonly ConcurrentDictionary<string, string> _connectionAuctions = new();

    // Track connections that joined the admin group (for cleanup on disconnect)
    private static readonly ConcurrentDictionary<string, byte> _adminConnectionIds = new();

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        _userConnections.TryAdd(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;

        // Remove from user group (explicit leave for clarity)
        if (_userConnections.TryRemove(connectionId, out var userId) && !string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(connectionId, GroupNames.User(userId));
        }

        // Remove from admin group if joined
        if (_adminConnectionIds.TryRemove(connectionId, out _))
        {
            await Groups.RemoveFromGroupAsync(connectionId, GroupNames.Admins);
        }

        // Remove from auction room if they were viewing one
        if (_connectionAuctions.TryRemove(connectionId, out var auctionId))
        {
            await LeaveAuctionGroup(auctionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinUserGroup()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return;
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.User(userId));
    }

    public async Task JoinAdminGroup()
    {
        var role = Context.User?.FindFirstValue(ClaimTypes.Role);
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Admins);
            _adminConnectionIds.TryAdd(Context.ConnectionId, 0);
        }
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

        // Join the new auction group (use GroupNames.Auction for consistency with AuctionController)
        var groupName = GroupNames.Auction(auctionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

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
        await Clients.Group(groupName).SendAsync("ViewerCountUpdated", new
        {
            AuctionId = auctionId,
            ViewerCount = viewerCount
        });
    }

    public async Task LeaveAuctionGroup(string auctionId)
    {
        if (string.IsNullOrWhiteSpace(auctionId))
            return;

        var groupName = GroupNames.Auction(auctionId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

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
                await Clients.Group(groupName).SendAsync("ViewerCountUpdated", new
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
        await hubContext.Clients.Group(GroupNames.Auction(auctionId)).SendAsync("UpdateBid", bidData);
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
        await hubContext.Clients.Group(GroupNames.Auction(auctionId)).SendAsync("AuctionEnded", endData);
    }

    // Notify when auction time is extended
    public static async Task NotifyTimeExtended(IHubContext<AuctionHub> hubContext, string auctionId, object extendData)
    {
        await hubContext.Clients.Group(GroupNames.Auction(auctionId)).SendAsync("TimeExtended", extendData);
    }

    // Notify when seller accepts current bid
    public static async Task NotifyAuctionAccepted(IHubContext<AuctionHub> hubContext, string auctionId, object acceptData)
    {
        await hubContext.Clients.Group(GroupNames.Auction(auctionId)).SendAsync("AuctionAccepted", acceptData);
    }

    // Notify when someone buyouts the auction
    public static async Task NotifyAuctionBuyout(IHubContext<AuctionHub> hubContext, string auctionId, object buyoutData)
    {
        await hubContext.Clients.Group(GroupNames.Auction(auctionId)).SendAsync("AuctionBuyout", buyoutData);
    }

    // Notify when auction is cancelled
    public static async Task NotifyAuctionCancelled(IHubContext<AuctionHub> hubContext, string auctionId, object cancelData)
    {
        await hubContext.Clients.Group(GroupNames.Auction(auctionId)).SendAsync("AuctionCancelled", cancelData);
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
