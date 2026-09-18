namespace Natoshare.Application.Subscriptions;

public record SubscriptionStatusDto(string Plan, string Status, bool IsTrial, DateTimeOffset TrialEndsAt, DateTimeOffset? PeriodEnd);

public record SubscriptionRecordDto(
    Guid Id,
    string Plan,
    string BillingCycle,
    string Status,
    string Reference,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? PeriodEnd);

public record UpgradeSubscriptionRequest(string Plan, string BillingCycle);

public record UpgradeResultDto(string Reference, string Status, string Message);

public record ActivateSubscriptionRequest(DateTimeOffset? PeriodEnd);
