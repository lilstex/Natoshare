namespace Natoshare.Application.Common;

// These come from the "Jwt" section of config. They control how we sign and check
// access tokens, and how long access and refresh tokens last before they expire.
public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}
