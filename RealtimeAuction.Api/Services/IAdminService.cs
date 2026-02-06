using RealtimeAuction.Api.Dtos.Admin;

namespace RealtimeAuction.Api.Services;

public interface IAdminService
{
    Task<UserListPaginatedResponse> GetUsersAsync(int page, int pageSize, string? search, string? role, bool? isLocked);
    Task<UserListResponse?> GetUserByIdAsync(string userId);
    Task<UserListResponse> CreateUserAsync(CreateUserRequest request);
    Task<UserListResponse> UpdateUserAsync(string userId, UpdateUserRequest request);
    Task<bool> DeleteUserAsync(string userId, string currentAdminId);
    Task<UserListResponse> LockUserAsync(string userId, LockUserRequest request, string currentAdminId);
    Task<UserListResponse> UnlockUserAsync(string userId);
    Task<UserListResponse> ChangeUserRoleAsync(string userId, ChangeRoleRequest request);
    Task<UserStatsResponse> GetUserStatsAsync();
    
    // Bulk actions
    Task<int> BulkLockUsersAsync(List<string> userIds, string? reason, string currentAdminId);
    Task<int> BulkUnlockUsersAsync(List<string> userIds);
    Task<int> BulkDeleteUsersAsync(List<string> userIds, string currentAdminId);
    Task<int> BulkChangeRoleAsync(List<string> userIds, string role);
}
