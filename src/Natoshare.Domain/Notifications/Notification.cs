namespace Natoshare.Domain.Notifications;

public enum NotificationSeverity
{
    Info,
    Warning,
    Critical,
}

// One thing Natoshare wants to tell a user about, like a category that has gone into
// deficit or is spending faster than it should. Written once and then just sits there
// until the user reads it, nothing here ever gets deleted.
public class Notification
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public NotificationKind Kind { get; init; }

    public string Title { get; init; } = "";

    public string Body { get; init; } = "";

    public NotificationSeverity Severity { get; init; }

    public string? RelatedEntityType { get; init; }

    public string? RelatedEntityId { get; init; }

    public bool IsRead { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
