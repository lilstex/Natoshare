using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.Subscriptions;

namespace Natoshare.Application.Admin;

// Everything the admin app's screens need, see docs/04-admin-app.md.

public record AdminUserListItemDto(
    Guid Id, string Email, string DisplayName, string Status, string Role, string Plan, bool IsTrial, DateTimeOffset CreatedAt);

public record AdminUserListResult(IReadOnlyList<AdminUserListItemDto> Items, int TotalCount, int Page, int PageSize);

public record AdminUserDetailDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Status,
    string Role,
    string TimeZoneId,
    string CurrencyCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset TrialEndsAt,
    DateTimeOffset? OnboardingCompletedAt,
    PlanEntitlements Entitlements,
    BalancesResult CurrentMonth,
    IReadOnlyList<SubscriptionRecordDto> SubscriptionHistory,
    IReadOnlyList<AdminAuditEventDto> RecentActivity,
    int DeficitResolutionCount,
    int ConfigVersionCount,
    int UnreadNotificationCount,
    int AlertPreferenceCount);

public record SuspendUserRequest(string Reason);

public record AdjustPlanRequest(string? Plan, DateTimeOffset? TrialEndsAt);

public record HardDeleteUserRequest(string ConfirmText);

public record AdminResetPasswordResult(string ResetToken);

public record AdminExportResult(Guid ExportId);

public record RecomputeDiffItemDto(Guid CategoryId, string CategoryName, decimal StoredSpent, decimal RecomputedSpent, bool HasDrift);

public record RecomputeBalancesResult(int Year, int Month, IReadOnlyList<RecomputeDiffItemDto> Diffs);

public record AdminAuditEventDto(
    Guid Id,
    Guid? ActorUserId,
    string ActorRole,
    string Action,
    string EntityType,
    string EntityId,
    string? Before,
    string? After,
    string? Ip,
    DateTimeOffset CreatedAt);

public record AdminAuditSearchResult(IReadOnlyList<AdminAuditEventDto> Items, int TotalCount, int Page, int PageSize);

public record AdminPlanConfigDto(
    string Plan, int? MaxCategories, int? HistoryWindowDays, bool SinkingFund, bool DeficitCoverFromSavings, bool Recurring, bool Export);

public record PatchPlanConfigRequest(
    int? MaxCategories, int? HistoryWindowDays, bool SinkingFund, bool DeficitCoverFromSavings, bool Recurring, bool Export);

public record AdminFeatureFlagDto(string Key, bool Enabled);

public record PatchFeatureFlagRequest(bool Enabled);

public record SystemSettingDto(string Key, string Value, DateTimeOffset UpdatedAt);

public record PatchSettingRequest(string Value);

public record AdminMetricsDto(
    int TotalUsers,
    int SignupsLast7Days,
    int SignupsLast30Days,
    int ActiveUsersLast30Days,
    int TrialUsers,
    int ProUsers,
    int SuspendedUsers,
    int PendingSubscriptions,
    double MonthCloseRateLast30Days);

public record AdminHealthDto(string DatabaseStatus, string HangfireStatus, DateTimeOffset CheckedAt);

public record AdminFailedJobDto(string JobId, string JobName, string? ExceptionMessage, DateTimeOffset? FailedAt);

public record AdminJobsDto(int EnqueuedCount, int ProcessingCount, int SucceededCount, int FailedCount, IReadOnlyList<AdminFailedJobDto> RecentFailures);

public record IntegrityCheckStatusDto(DateTimeOffset? LastRanAt, int? LastCheckedCount, int? LastDriftCount);

public record AdminErrorLogEntryDto(DateTimeOffset Timestamp, string Level, string Message, string? Exception);
