namespace Natoshare.Domain.Identity;

// A short-lived code used to prove someone owns an email address, so they can set a
// new password after forgetting the old one. We only ever store the hash of the code,
// never the real value, same reason as RefreshToken.
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Tells us if this code can still be used to reset the password right now.
    public bool IsUsable(DateTimeOffset now) => UsedAt is null && ExpiresAt > now;
}
