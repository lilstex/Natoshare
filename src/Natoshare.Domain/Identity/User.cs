using Microsoft.AspNetCore.Identity;

namespace Natoshare.Domain.Identity;

// This is a Natoshare user. The login fields (email, password hash, and so on) come
// from IdentityUser, that part is handled for us. Everything below is what Natoshare
// itself needs on top of that, like the user's currency, timezone and trial.
public class User : IdentityUser<Guid>
{
    // The name we show the user in the app, for example "Amara".
    public string DisplayName { get; set; } = string.Empty;

    // Which currency this account uses. Any ISO 4217 code works, NGN is only the
    // default. This gets locked once the user logs their first income or expense,
    // that locking happens in Phase 3, not here.
    public string CurrencyCode { get; set; } = "NGN";
    public string CurrencySymbol { get; set; } = "₦";
    public bool CurrencyLocked { get; set; }

    // The user's timezone (an IANA id like "Africa/Lagos"). This drives things like
    // "days left in the month" further down the line.
    public string TimeZoneId { get; set; } = "Africa/Lagos";

    // Used to format numbers, money and dates the way this user expects to see them.
    public string Locale { get; set; } = "en-NG";

    public UserStatus Status { get; set; } = UserStatus.Active;

    // Every new signup gets a free trial. This is when it runs out. After Phase 9 this
    // will matter for what the user can and cannot do, for now it is just recorded.
    public DateTimeOffset TrialEndsAt { get; set; }

    // Set once the user finishes the onboarding wizard in Phase 2. Stays null until then.
    public DateTimeOffset? OnboardingCompletedAt { get; set; }

    // Set the moment DELETE /me moves this account to PendingDeletion, so
    // PurgePendingDeletions knows when the grace period is actually over.
    public DateTimeOffset? PendingDeletionRequestedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
