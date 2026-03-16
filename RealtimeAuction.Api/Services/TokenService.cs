using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using RealtimeAuction.Api.Models;
using RealtimeAuction.Api.Settings;

namespace RealtimeAuction.Api.Services
{
    public class TokenService : ITokenService
    {
        private readonly JwtSettings _jwtSettings;
        private readonly IMongoCollection<RefreshToken> _refreshTokenCollection;

        public TokenService(IOptions<JwtSettings> jwtSettings, IMongoDatabase database)
        {
            _jwtSettings = jwtSettings.Value;
            _refreshTokenCollection = database.GetCollection<RefreshToken>("RefreshTokens");
        }

        public string GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role ?? "User")
                }),
                Expires = DateTime.UtcNow.AddMinutes(15),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var refreshToken = new RefreshToken
            {
                TokenHash = HashToken(rawToken),
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Revoked = false,
                CreatedAt = DateTime.UtcNow,
                PlainTextToken = rawToken
            };

            await _refreshTokenCollection.InsertOneAsync(refreshToken);
            return refreshToken;
        }

        public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
        {
            var tokenHash = HashToken(token);
            var filter = Builders<RefreshToken>.Filter.And(
                Builders<RefreshToken>.Filter.Eq(x => x.Revoked, false),
                Builders<RefreshToken>.Filter.Or(
                    Builders<RefreshToken>.Filter.Eq(x => x.TokenHash, tokenHash),
                    Builders<RefreshToken>.Filter.Eq(x => x.Token, token)
                ));

            var refreshToken = await _refreshTokenCollection.Find(filter).FirstOrDefaultAsync();

            if (refreshToken == null || refreshToken.ExpiresAt <= DateTime.UtcNow)
            {
                return null;
            }

            return refreshToken;
        }

        public async Task RevokeRefreshTokenAsync(string token)
        {
            var tokenHash = HashToken(token);
            var filter = Builders<RefreshToken>.Filter.Or(
                Builders<RefreshToken>.Filter.Eq(x => x.TokenHash, tokenHash),
                Builders<RefreshToken>.Filter.Eq(x => x.Token, token)
            );

            var update = Builders<RefreshToken>.Update
                .Set(x => x.Revoked, true)
                .Unset(x => x.Token);

            await _refreshTokenCollection.UpdateOneAsync(filter, update);
        }

        private static string HashToken(string token)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(hash);
        }
    }
}
