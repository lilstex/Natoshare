namespace Natoshare.Domain.Subscriptions;

public enum SubscriptionStatus
{
    // Created by the user, waiting on an admin (payments are stubbed, see
    // docs/00-plan.md section 5, there is no real payment gateway in v1).
    Pending,

    Active,
    Cancelled,

    // An Active record whose PeriodEnd has passed, set by ExpireTrialsAndSubscriptions.
    Expired,
}

public enum BillingCycle
{
    Monthly,
    Annual,
}

// One request to go Pro, and (once an admin activates it) the record of actually
// being Pro for a while. A user can have many of these over time, GetHistoryAsync
// shows all of them, only the latest Active one (if any) counts for entitlements.
public class SubscriptionRecord
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public PlanTier Plan { get; init; }

    public BillingCycle BillingCycle { get; init; }

    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;

    // Shown to the user so they have something to reference when asking about their
    // upgrade, and used by the admin activation endpoint to find the right record.
    public string Reference { get; init; } = "";

    public DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public Guid? ActivatedByAdminUserId { get; set; }

    // Null means "does not expire on its own", only ever set by an admin at
    // activation time.
    public DateTimeOffset? PeriodEnd { get; set; }
}
