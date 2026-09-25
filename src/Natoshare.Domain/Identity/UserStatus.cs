namespace Natoshare.Domain.Identity;

// The state a user's account can be in.
public enum UserStatus
{
    // Everything is normal, the user can log in and use the app.
    Active,

    // An admin has switched off this account. The user cannot log in until an admin
    // turns it back on.
    Suspended,

    // The user asked to delete their account. We keep the data for a short grace
    // period before a background job removes it for good.
    PendingDeletion
}
