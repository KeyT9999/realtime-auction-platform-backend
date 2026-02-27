namespace RealtimeAuction.Api.Settings;

/// <summary>
/// Realtime = SignalR only. When user is offline, send email if this is enabled.
/// </summary>
public class NotificationSettings
{
    public const string SectionName = "Notifications";

    /// <summary>
    /// If true, send email to users when they are offline (e.g. outbid, ending soon).
    /// Realtime notifications always go via SignalR when user is online.
    /// </summary>
    public bool SendEmailWhenOffline { get; set; } = true;
}
