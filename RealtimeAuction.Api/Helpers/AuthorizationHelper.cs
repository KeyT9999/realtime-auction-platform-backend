namespace RealtimeAuction.Api.Helpers;

public static class AuthorizationHelper
{
    public static bool IsOwnerOrAdmin(string? userId, string? resourceUserId, string? userRole)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(resourceUserId))
        {
            return false;
        }

        // Admin can access any resource
        if (userRole == "Admin")
        {
            return true;
        }

        // User can only access their own resources
        return userId == resourceUserId;
    }

    public static void RequireOwnerOrAdmin(string? userId, string? resourceUserId, string? userRole)
    {
        if (!IsOwnerOrAdmin(userId, resourceUserId, userRole))
        {
            throw new UnauthorizedAccessException("You do not have permission to access this resource.");
        }
    }
}
