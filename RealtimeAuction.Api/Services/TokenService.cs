using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RealtimeAuction.Api.Models;
using Microsoft.Extensions.Options;
using RealtimeAuction.Api.Settings;
using MongoDB.Driver;

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
                    new Claim(ClaimTypes.NameIdentifier, user.Id ?? ""),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Name, user.FullName),
                    new Claim(ClaimTypes.Role, user.Role ?? "User")
                }),
                Expires = DateTime.UtcNow.AddMinutes(15), // Access token short lifespan
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public RefreshToken GenerateRefreshToken(string userId)
        {
            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            };

            _refreshTokenCollection.InsertOne(refreshToken);
            return refreshToken;
        }

        public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
        {
            var refreshToken = await _refreshTokenCollection.Find(x => x.Token == token && !x.Revoked).FirstOrDefaultAsync();

            if (refreshToken == null || refreshToken.ExpiresAt <= DateTime.UtcNow)
            {
                return null;
            }

            return refreshToken;
        }

        public async Task RevokeRefreshTokenAsync(string token)
        {
            var update = Builders<RefreshToken>.Update.Set(x => x.Revoked, true);
            await _refreshTokenCollection.UpdateOneAsync(x => x.Token == token, update);
        }
    }
}
