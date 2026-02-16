namespace RealtimeAuction.Api.Hubs;

public static class GroupNames
{
    public const string Admins = "admins";

    public static string Auction(string auctionId) => $"auction-{auctionId}";

    public static string User(string userId) => $"user-{userId}";
}


