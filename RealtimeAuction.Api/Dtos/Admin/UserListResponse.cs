namespace RealtimeAuction.Api.Dtos.Admin;

public class UserListResponse
{
    public string Id { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public bool IsEmailVerified { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? LockedReason { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal EscrowBalance { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
