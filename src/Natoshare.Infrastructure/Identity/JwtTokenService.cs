using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Natoshare.Application.Auth;
using Natoshare.Application.Common;
using Natoshare.Domain.Identity;

namespace Natoshare.Infrastructure.Identity;

// Builds the access tokens and refresh tokens that keep a user signed in, using the
// settings from the "Jwt" section of config.
public class JwtTokenService : ITokenService
{
    private readonly JwtSettings _settings;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtSettings> settings, IClock clock)
    {
        _settings = settings.Value;
        _clock = clock;
    }

    public string CreateAccessToken(User user, string role)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        // These are the facts about the user that we put inside the token itself, so
        // the API can trust them without asking the database on every single request.
        // "plan" is hard-coded to Free for now, Phase 9 is what makes real plans work.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new("name", user.DisplayName),
            new(ClaimTypes.Role, role),
            new("plan", "Free"),
            new("trialEndsAt", user.TrialEndsAt.ToUnixTimeSeconds().ToString()),
        };

        var now = _clock.UtcNow;
        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: now.AddMinutes(_settings.AccessTokenMinutes).UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public NewRefreshToken CreateRefreshToken()
    {
        // A long random string nobody could guess. We hand this raw value to the
        // caller once, and only ever keep its hash in the database.
        var rawToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = _clock.UtcNow.AddDays(_settings.RefreshTokenDays);

        return new NewRefreshToken(rawToken, Hash(rawToken), expiresAt);
    }

    public string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
