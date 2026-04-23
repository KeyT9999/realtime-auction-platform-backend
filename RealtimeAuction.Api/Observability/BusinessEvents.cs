namespace RealtimeAuction.Api.Observability;

public static class BusinessEvents
{
    public const string BidReceived = "BidReceived";
    public const string BidRejected = "BidRejected";
    public const string BidAccepted = "BidAccepted";
    public const string BidProcessingFallback = "BidProcessingFallback";
    public const string BidProcessingFailed = "BidProcessingFailed";

    public const string AuctionEndProcessingStarted = "AuctionEndProcessingStarted";
    public const string AuctionClosedNoBids = "AuctionClosedNoBids";
    public const string AuctionClosedWinnerSelected = "AuctionClosedWinnerSelected";

    public const string DisputeCreated = "DisputeCreated";
    public const string DisputeMessageAdded = "DisputeMessageAdded";
    public const string DisputeUnderReview = "DisputeUnderReview";
    public const string DisputeResolved = "DisputeResolved";
    public const string DisputeClosed = "DisputeClosed";
    public const string DisputeNotificationFailed = "DisputeNotificationFailed";
    public const string DisputeEscrowActionFailed = "DisputeEscrowActionFailed";
}

public static class AuditLog
{
    public static long ToUnixTimeMilliseconds(DateTime value)
    {
        var utcValue = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTimeOffset(utcValue).ToUnixTimeMilliseconds();
    }
}
