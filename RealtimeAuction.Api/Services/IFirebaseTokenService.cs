namespace RealtimeAuction.Api.Services;

public interface IFirebaseTokenService
{
    Task<string> CreateCustomTokenAsync(
        string userId,
        IDictionary<string, object?> claims,
        CancellationToken cancellationToken = default);
}
