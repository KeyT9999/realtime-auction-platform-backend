// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho BulkChangeRoleRequest.
namespace RealtimeAuction.Api.Dtos.Admin;

public class BulkChangeRoleRequest
{
    public required List<string> UserIds { get; set; }
    public required string Role { get; set; }
}
