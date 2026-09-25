using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Identity;
using Natoshare.Domain.Subscriptions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Admin;

// What the admin app's monitoring screen shows. Metrics and health are worked out
// fresh from the database and the health check service every time this is called,
// nothing here is cached, so there is one, always-current source of truth.
public class AdminMonitoringService : IAdminMonitoringService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly HealthCheckService _healthCheckService;

    public AdminMonitoringService(NatoshareDbContext dbContext, IClock clock, HealthCheckService healthCheckService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _healthCheckService = healthCheckService;
    }

    public async Task<AdminMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var last7Days = now.AddDays(-7);
        var last30Days = now.AddDays(-30);

        var totalUsers = await _dbContext.Users.CountAsync(cancellationToken);
        var signupsLast7 = await _dbContext.Users.CountAsync(u => u.CreatedAt >= last7Days, cancellationToken);
        var signupsLast30 = await _dbContext.Users.CountAsync(u => u.CreatedAt >= last30Days, cancellationToken);
        var suspendedUsers = await _dbContext.Users.CountAsync(u => u.Status == UserStatus.Suspended, cancellationToken);
        var trialUsers = await _dbContext.Users.CountAsync(u => u.Status == UserStatus.Active && u.TrialEndsAt > now, cancellationToken);

        var activeProUserIds = await _dbContext.SubscriptionRecords
            .Where(s => s.Status == SubscriptionStatus.Active && (s.PeriodEnd == null || s.PeriodEnd > now))
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var pendingSubscriptions = await _dbContext.SubscriptionRecords.CountAsync(s => s.Status == SubscriptionStatus.Pending, cancellationToken);

        // "Active" here means logged in at all in the last 30 days, worked out from
        // the audit log's UserLoggedIn events rather than a separate LastSeenAt
        // column we do not have.
        var activeUsersLast30Days = await _dbContext.AuditEvents
            .Where(a => a.Action == "UserLoggedIn" && a.CreatedAt >= last30Days && a.ActorUserId != null)
            .Select(a => a.ActorUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var monthsClosedLast30Days = await _dbContext.BudgetMonths.CountAsync(
            m => m.Status == BudgetMonthStatus.Closed && m.ClosedAt != null && m.ClosedAt >= last30Days, cancellationToken);
        var monthsOpenedLast30Days = await _dbContext.BudgetMonths.CountAsync(m => m.OpenedAt >= last30Days, cancellationToken);
        var closeRate = monthsOpenedLast30Days == 0 ? 0d : (double)monthsClosedLast30Days / monthsOpenedLast30Days;

        return new AdminMetricsDto(
            totalUsers, signupsLast7, signupsLast30, activeUsersLast30Days, trialUsers, activeProUserIds, suspendedUsers,
            pendingSubscriptions, Math.Round(closeRate, 2));
    }

    public async Task<AdminHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);
        var databaseStatus = report.Entries.TryGetValue("postgres", out var entry) ? entry.Status.ToString() : "Unknown";

        var hangfireStatus = "Unknown";
        try
        {
            JobStorage.Current.GetMonitoringApi().Servers();
            hangfireStatus = "Healthy";
        }
        catch (Exception)
        {
            hangfireStatus = "Unhealthy";
        }

        return new AdminHealthDto(databaseStatus, hangfireStatus, _clock.UtcNow);
    }

    public AdminJobsDto GetJobs()
    {
        var api = JobStorage.Current.GetMonitoringApi();

        var recentFailures = api.FailedJobs(0, 20)
            .Select(pair => new AdminFailedJobDto(
                pair.Key,
                pair.Value.Job is not null ? $"{pair.Value.Job.Type.Name}.{pair.Value.Job.Method.Name}" : "(unknown job)",
                pair.Value.ExceptionMessage,
                pair.Value.FailedAt is null ? null : new DateTimeOffset(DateTime.SpecifyKind(pair.Value.FailedAt.Value, DateTimeKind.Utc))))
            .ToList();

        return new AdminJobsDto(
            (int)api.EnqueuedCount("default"), (int)api.ProcessingCount(), (int)api.SucceededListCount(), (int)api.FailedCount(), recentFailures);
    }

    public async Task<IntegrityCheckStatusDto> GetIntegrityStatusAsync(CancellationToken cancellationToken = default)
    {
        var lastRun = await _dbContext.IntegrityCheckRuns.OrderByDescending(r => r.RanAt).FirstOrDefaultAsync(cancellationToken);

        return lastRun is null
            ? new IntegrityCheckStatusDto(null, null, null)
            : new IntegrityCheckStatusDto(lastRun.RanAt, lastRun.CheckedCount, lastRun.DriftCount);
    }

    public IReadOnlyList<AdminErrorLogEntryDto> GetRecentErrors() => InMemoryErrorSink.GetRecent();
}
