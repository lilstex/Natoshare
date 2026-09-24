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

// Same shape as SubscriptionRecordDto, plus who the record belongs to, for the admin
// app's subscriptions queue where records from many different users are listed together.
public record AdminSubscriptionRecordDto(
    Guid Id,
    Guid UserId,
    string UserEmail,
    string UserDisplayName,
    string Plan,
    string BillingCycle,
    string Status,
    string Reference,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ActivatedAt,
    DateTimeOffset? PeriodEnd);
