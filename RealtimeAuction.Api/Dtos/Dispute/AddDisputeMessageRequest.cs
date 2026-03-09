namespace RealtimeAuction.Api.Dtos.Dispute;

public class AddDisputeMessageRequest
{
    public string Content { get; set; } = null!;
    public List<string> Attachments { get; set; } = new();
}
