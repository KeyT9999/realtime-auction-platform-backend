// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho UpdateAuctionStatusDto.
using System.ComponentModel.DataAnnotations;
using RealtimeAuction.Api.Models.Enums;

namespace RealtimeAuction.Api.Dtos.Auction;

public class UpdateAuctionStatusDto
{
    [Required]
    public AuctionStatus Status { get; set; }
}
