namespace Natoshare.Domain.Audit;

// One row for every important thing that happens in Natoshare, like someone logging in
// or an admin suspending a user. This is how we can always answer "who did what, and
// when", which matters a lot for a money app even though we never hold real money.
public class AuditEvent
{
    public Guid Id { get; set; }

    // Who did this. Null for things that happen before we know who the user is yet,
    // for example a failed login attempt.
    public Guid? ActorUserId { get; set; }
    public string ActorRole { get; set; } = "Anonymous";

    // A short name for what happened, for example "UserLoggedIn" or "PasswordChanged".
    public string Action { get; set; } = string.Empty;

    // What kind of thing this event is about, and its id, for example "User" and the
    // user's own id.
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;

    // What the entity looked like before and after, when that is worth keeping.
    // These stay null until we start tracking real entity changes in later phases.
    public string? Before { get; set; }
    public string? After { get; set; }

    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
