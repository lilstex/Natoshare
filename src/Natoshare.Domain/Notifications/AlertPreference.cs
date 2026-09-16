namespace Natoshare.Domain.Notifications;

// How loud one kind of alert should be for this user: on or off, and (for the kinds
// that use one) a threshold that decides when it fires. Seeded with sensible defaults
// the moment an account is created, so alerts work out of the box without the user
// having to set anything up first.
public class AlertPreference
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public NotificationKind Kind { get; init; }

    public bool Enabled { get; set; } = true;

    public decimal? ThresholdPercent { get; set; }

    public int? LeadDays { get; set; }
}
