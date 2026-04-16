// Mục đích tệp: Dinh nghia du lieu trao doi (DTO) cho UserStatsResponse.
namespace RealtimeAuction.Api.Dtos.Admin;

public class UserStatsResponse
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int AdminUsers { get; set; }
    public int RegularUsers { get; set; }
    public int VerifiedUsers { get; set; }
    public int UnverifiedUsers { get; set; }
}
