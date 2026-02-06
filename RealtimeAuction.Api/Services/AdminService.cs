using MongoDB.Driver;
using RealtimeAuction.Api.Dtos.Admin;
using RealtimeAuction.Api.Helpers;
using RealtimeAuction.Api.Models;
using BCrypt.Net;

namespace RealtimeAuction.Api.Services;

public class AdminService : IAdminService
{
    private readonly IMongoCollection<User> _users;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IMongoDatabase database, ILogger<AdminService> logger)
    {
        _users = database.GetCollection<User>("Users");
        _logger = logger;
    }

    public async Task<UserListPaginatedResponse> GetUsersAsync(int page, int pageSize, string? search, string? role, bool? isLocked)
    {
        var filterBuilder = Builders<User>.Filter;
        var filters = new List<FilterDefinition<User>>();

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchFilter = filterBuilder.Or(
                filterBuilder.Regex(u => u.Email, new MongoDB.Bson.BsonRegularExpression(search, "i")),
                filterBuilder.Regex(u => u.FullName, new MongoDB.Bson.BsonRegularExpression(search, "i"))
            );
            filters.Add(searchFilter);
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(role))
        {
            filters.Add(filterBuilder.Eq(u => u.Role, role));
        }

        // Locked filter
        if (isLocked.HasValue)
        {
            filters.Add(filterBuilder.Eq(u => u.IsLocked, isLocked.Value));
        }

        var filter = filters.Count > 0 ? filterBuilder.And(filters) : filterBuilder.Empty;

        var totalCount = await _users.CountDocumentsAsync(filter);
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var users = await _users
            .Find(filter)
            .SortByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        var userResponses = users.Select(u => new UserListResponse
        {
            Id = u.Id!,
            Email = u.Email,
            FullName = u.FullName,
            Role = u.Role ?? "User",
            IsEmailVerified = u.IsEmailVerified,
            IsLocked = u.IsLocked,
            LockedAt = u.LockedAt,
            LockedReason = u.LockedReason,
            Phone = u.Phone,
            Address = u.Address,
            CreatedAt = u.CreatedAt,
            UpdatedAt = u.UpdatedAt
        }).ToList();

        return new UserListPaginatedResponse
        {
            Users = userResponses,
            TotalCount = (int)totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        };
    }

    public async Task<UserListResponse?> GetUserByIdAsync(string userId)
    {
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null) return null;

        return new UserListResponse
        {
            Id = user.Id!,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role ?? "User",
            IsEmailVerified = user.IsEmailVerified,
            IsLocked = user.IsLocked,
            LockedAt = user.LockedAt,
            LockedReason = user.LockedReason,
            Phone = user.Phone,
            Address = user.Address,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<UserListResponse> CreateUserAsync(CreateUserRequest request)
    {
        // Check if email already exists
        var existingUser = await _users.Find(u => u.Email == request.Email).FirstOrDefaultAsync();
        if (existingUser != null)
        {
            throw new Exception("Email already exists");
        }

        // Validate password strength
        var passwordValidation = PasswordValidator.ValidatePasswordStrength(request.Password);
        if (!passwordValidation.IsValid)
        {
            throw new Exception(passwordValidation.ErrorMessage);
        }

        // Validate role
        if (request.Role != "User" && request.Role != "Admin")
        {
            throw new Exception("Invalid role. Role must be 'User' or 'Admin'");
        }

        var user = new User
        {
            Email = request.Email,
            FullName = request.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            Phone = request.Phone,
            Address = request.Address,
            IsEmailVerified = false,
            IsLocked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _users.InsertOneAsync(user);

        _logger.LogInformation("Admin created user: {Email} with role: {Role}", user.Email, user.Role);

        return new UserListResponse
        {
            Id = user.Id!,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role ?? "User",
            IsEmailVerified = user.IsEmailVerified,
            IsLocked = user.IsLocked,
            Phone = user.Phone,
            Address = user.Address,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<UserListResponse> UpdateUserAsync(string userId, UpdateUserRequest request)
    {
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new Exception("User not found");
        }

        // Validate role if provided
        if (!string.IsNullOrWhiteSpace(request.Role) && request.Role != "User" && request.Role != "Admin")
        {
            throw new Exception("Invalid role. Role must be 'User' or 'Admin'");
        }

        user.FullName = request.FullName;
        user.Phone = request.Phone;
        user.Address = request.Address;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            user.Role = request.Role;
        }
        user.UpdatedAt = DateTime.UtcNow;

        await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

        _logger.LogInformation("Admin updated user: {UserId}", userId);

        return new UserListResponse
        {
            Id = user.Id!,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role ?? "User",
            IsEmailVerified = user.IsEmailVerified,
            IsLocked = user.IsLocked,
            LockedAt = user.LockedAt,
            LockedReason = user.LockedReason,
            Phone = user.Phone,
            Address = user.Address,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<bool> DeleteUserAsync(string userId, string currentAdminId)
    {
        // Prevent self-deletion
        if (userId == currentAdminId)
        {
            throw new Exception("Cannot delete your own account");
        }

        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new Exception("User not found");
        }

        var result = await _users.DeleteOneAsync(u => u.Id == userId);
        _logger.LogInformation("Admin deleted user: {UserId}", userId);
        return result.DeletedCount > 0;
    }

    public async Task<UserListResponse> LockUserAsync(string userId, LockUserRequest request, string currentAdminId)
    {
        // Prevent self-locking
        if (userId == currentAdminId)
        {
            throw new Exception("Cannot lock your own account");
        }

        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new Exception("User not found");
        }

        if (user.IsLocked)
        {
            throw new Exception("User is already locked");
        }

        user.IsLocked = true;
        user.LockedAt = DateTime.UtcNow;
        user.LockedReason = request.Reason;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

        _logger.LogInformation("Admin locked user: {UserId}, Reason: {Reason}", userId, request.Reason);

        return new UserListResponse
        {
            Id = user.Id!,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role ?? "User",
            IsEmailVerified = user.IsEmailVerified,
            IsLocked = user.IsLocked,
            LockedAt = user.LockedAt,
            LockedReason = user.LockedReason,
            Phone = user.Phone,
            Address = user.Address,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<UserListResponse> UnlockUserAsync(string userId)
    {
        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new Exception("User not found");
        }

        if (!user.IsLocked)
        {
            throw new Exception("User is not locked");
        }

        user.IsLocked = false;
        user.LockedAt = null;
        user.LockedReason = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

        _logger.LogInformation("Admin unlocked user: {UserId}", userId);

        return new UserListResponse
        {
            Id = user.Id!,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role ?? "User",
            IsEmailVerified = user.IsEmailVerified,
            IsLocked = user.IsLocked,
            LockedAt = user.LockedAt,
            LockedReason = user.LockedReason,
            Phone = user.Phone,
            Address = user.Address,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<UserListResponse> ChangeUserRoleAsync(string userId, ChangeRoleRequest request)
    {
        if (request.Role != "User" && request.Role != "Admin")
        {
            throw new Exception("Invalid role. Role must be 'User' or 'Admin'");
        }

        var user = await _users.Find(u => u.Id == userId).FirstOrDefaultAsync();
        if (user == null)
        {
            throw new Exception("User not found");
        }

        user.Role = request.Role;
        user.UpdatedAt = DateTime.UtcNow;

        await _users.ReplaceOneAsync(u => u.Id == user.Id, user);

        _logger.LogInformation("Admin changed role for user: {UserId} to {Role}", userId, request.Role);

        return new UserListResponse
        {
            Id = user.Id!,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role ?? "User",
            IsEmailVerified = user.IsEmailVerified,
            IsLocked = user.IsLocked,
            LockedAt = user.LockedAt,
            LockedReason = user.LockedReason,
            Phone = user.Phone,
            Address = user.Address,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<UserStatsResponse> GetUserStatsAsync()
    {
        var totalUsers = await _users.CountDocumentsAsync(_ => true);
        var activeUsers = await _users.CountDocumentsAsync(u => !u.IsLocked);
        var lockedUsers = await _users.CountDocumentsAsync(u => u.IsLocked);
        var adminUsers = await _users.CountDocumentsAsync(u => u.Role == "Admin");
        var regularUsers = await _users.CountDocumentsAsync(u => u.Role == "User" || u.Role == null);
        var verifiedUsers = await _users.CountDocumentsAsync(u => u.IsEmailVerified);
        var unverifiedUsers = await _users.CountDocumentsAsync(u => !u.IsEmailVerified);

        return new UserStatsResponse
        {
            TotalUsers = (int)totalUsers,
            ActiveUsers = (int)activeUsers,
            LockedUsers = (int)lockedUsers,
            AdminUsers = (int)adminUsers,
            RegularUsers = (int)regularUsers,
            VerifiedUsers = (int)verifiedUsers,
            UnverifiedUsers = (int)unverifiedUsers
        };
    }

    public async Task<int> BulkLockUsersAsync(List<string> userIds, string? reason, string currentAdminId)
    {
        // Remove current admin from the list to prevent self-locking
        userIds = userIds.Where(id => id != currentAdminId).ToList();
        
        var filter = Builders<User>.Filter.In(u => u.Id, userIds) & Builders<User>.Filter.Eq(u => u.IsLocked, false);
        
        var update = Builders<User>.Update
            .Set(u => u.IsLocked, true)
            .Set(u => u.LockedAt, DateTime.UtcNow)
            .Set(u => u.LockedReason, reason)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
            
        var result = await _users.UpdateManyAsync(filter, update);
        
        _logger.LogInformation("Admin bulk locked {Count} users. Reason: {Reason}", result.ModifiedCount, reason);
        
        return (int)result.ModifiedCount;
    }

    public async Task<int> BulkUnlockUsersAsync(List<string> userIds)
    {
        var filter = Builders<User>.Filter.In(u => u.Id, userIds) & Builders<User>.Filter.Eq(u => u.IsLocked, true);
        
        var update = Builders<User>.Update
            .Set(u => u.IsLocked, false)
            .Set(u => u.LockedAt, null)
            .Set(u => u.LockedReason, null)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
            
        var result = await _users.UpdateManyAsync(filter, update);
        
        _logger.LogInformation("Admin bulk unlocked {Count} users", result.ModifiedCount);
        
        return (int)result.ModifiedCount;
    }

    public async Task<int> BulkDeleteUsersAsync(List<string> userIds, string currentAdminId)
    {
        // Remove current admin from the list to prevent self-deletion
        userIds = userIds.Where(id => id != currentAdminId).ToList();
        
        var filter = Builders<User>.Filter.In(u => u.Id, userIds);
        var result = await _users.DeleteManyAsync(filter);
        
        _logger.LogInformation("Admin bulk deleted {Count} users", result.DeletedCount);
        
        return (int)result.DeletedCount;
    }

    public async Task<int> BulkChangeRoleAsync(List<string> userIds, string role)
    {
        if (role != "User" && role != "Admin")
        {
            throw new Exception("Invalid role. Role must be 'User' or 'Admin'");
        }
        
        var filter = Builders<User>.Filter.In(u => u.Id, userIds);
        
        var update = Builders<User>.Update
            .Set(u => u.Role, role)
            .Set(u => u.UpdatedAt, DateTime.UtcNow);
            
        var result = await _users.UpdateManyAsync(filter, update);
        
        _logger.LogInformation("Admin bulk changed role to {Role} for {Count} users", role, result.ModifiedCount);
        
        return (int)result.ModifiedCount;
    }
}
