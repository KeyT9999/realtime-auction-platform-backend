namespace RealtimeAuction.Api.Dtos.Admin;

public class BulkChangeRoleRequest
{
    public required List<string> UserIds { get; set; }
    public required string Role { get; set; }
}
