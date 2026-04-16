// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho BulkUserActionRequest.
namespace RealtimeAuction.Api.Dtos.Admin;

public class BulkUserActionRequest
{
    public required List<string> UserIds { get; set; }
    public string? Reason { get; set; }
}
