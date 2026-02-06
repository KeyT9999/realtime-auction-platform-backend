namespace RealtimeAuction.Api.Dtos.Admin;

public class BulkUserActionRequest
{
    public required List<string> UserIds { get; set; }
    public string? Reason { get; set; }
}
