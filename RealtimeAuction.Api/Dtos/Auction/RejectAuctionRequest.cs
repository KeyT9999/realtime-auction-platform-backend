// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho RejectAuctionRequest.
namespace RealtimeAuction.Api.Dtos.Auction;

public class RejectAuctionRequest
{
    public string Reason { get; set; } = null!;
}
