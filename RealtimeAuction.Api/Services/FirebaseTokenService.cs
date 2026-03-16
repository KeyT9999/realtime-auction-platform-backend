using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Services;

public class FirebaseTokenService : IFirebaseTokenService
{
    private const string FirebaseAudience = "https://identitytoolkit.googleapis.com/google.identity.identitytoolkit.v1.IdentityToolkit";
    private readonly FirebaseAuthSettings _settings;

    public FirebaseTokenService(IOptions<FirebaseAuthSettings> settings)
    {
        _settings = settings.Value;
    }

    private static string NormalizePrivateKey(string privateKey)
    {
        var normalized = privateKey.Replace("\\n", "\n").Replace("\r\n", "\n").Replace('\r', '\n').Trim();

        if (normalized.StartsWith('"') && normalized.EndsWith('"') && normalized.Length > 1)
        {
            normalized = normalized[1..^1];
        }

        if (!normalized.EndsWith('\n'))
        {
            normalized += '\n';
        }

        return normalized;
    }

    public Task<string> CreateCustomTokenAsync(
        string userId,
        IDictionary<string, object?> claims,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ProjectId) ||
            string.IsNullOrWhiteSpace(_settings.ClientEmail) ||
            string.IsNullOrWhiteSpace(_settings.PrivateKey))
        {
            throw new InvalidOperationException("Firebase custom token settings are not configured.");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required to create a Firebase custom token.", nameof(userId));
        }

        var privateKeyPem = NormalizePrivateKey(_settings.PrivateKey);

        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var securityKey = new RsaSecurityKey(rsa.ExportParameters(true))
        {
            CryptoProviderFactory = new CryptoProviderFactory
            {
                CacheSignatureProviders = false
            }
        };

        var now = DateTimeOffset.UtcNow;
        var payload = new JwtPayload
        {
            { "iss", _settings.ClientEmail },
            { "sub", _settings.ClientEmail },
            { "aud", FirebaseAudience },
            { "iat", now.ToUnixTimeSeconds() },
            { "exp", now.AddHours(1).ToUnixTimeSeconds() },
            { "uid", userId }
        };

        var filteredClaims = claims
            .Where(kvp => kvp.Value is not null)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (filteredClaims.Count > 0)
        {
            payload["claims"] = filteredClaims;
        }

        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        var token = new JwtSecurityToken(new JwtHeader(credentials), payload);
        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

        return Task.FromResult(tokenString);
    }
}
