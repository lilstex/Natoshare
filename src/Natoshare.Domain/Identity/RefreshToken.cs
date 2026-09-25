namespace Natoshare.Domain.Identity;

// A refresh token lets a user get a new access token without typing their password
// again. We never store the raw token, only its hash, so even if our database leaked,
// nobody could use it to log in as someone.
public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }

    // When a token is used to get a new one, we keep a link to the new token's hash
    // here. This helps us spot a stolen and reused old token later if we ever need to.
    public string? ReplacedByTokenHash { get; set; }

    public string? CreatedByIp { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Tells us if this token can still be used right now.
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
