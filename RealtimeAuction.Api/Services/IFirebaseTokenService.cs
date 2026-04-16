// Mục đích tệp: Chua logic nghiep vu chinh cho phan IFirebaseTokenService.
namespace RealtimeAuction.Api.Services;

public interface IFirebaseTokenService
{
    Task<string> CreateCustomTokenAsync(
        string userId,
        IDictionary<string, object?> claims,
        CancellationToken cancellationToken = default);
}
