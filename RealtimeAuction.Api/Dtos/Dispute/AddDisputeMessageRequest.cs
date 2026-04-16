// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho AddDisputeMessageRequest.
namespace RealtimeAuction.Api.Dtos.Dispute;

public class AddDisputeMessageRequest
{
    public string Content { get; set; } = null!;
    public List<string> Attachments { get; set; } = new();
}
