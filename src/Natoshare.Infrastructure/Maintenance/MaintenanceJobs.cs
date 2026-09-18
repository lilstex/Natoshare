using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.Maintenance;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Subscriptions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Maintenance;

public class MaintenanceJobs : IMaintenanceJobs
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly UserManager<User> _userManager;
    private readonly AppDefaults _appDefaults;
    private readonly ILogger<MaintenanceJobs> _logger;

    public MaintenanceJobs(
        NatoshareDbContext dbContext,
        IClock clock,
        ILedgerService ledgerService,
        UserManager<User> userManager,
        IOptions<AppDefaults> appDefaults,
        ILogger<MaintenanceJobs> logger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _userManager = userManager;
        _appDefaults = appDefaults.Value;
        _logger = logger;
    }

    public async Task ExpireTrialsAndSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        // A stale Active record (its PeriodEnd has passed with nothing renewing it,
        // payments are stubbed in v1 so nothing ever auto-renews) needs to actually
        // flip to Expired, otherwise it would keep counting as an active
        // subscription forever as far as IEntitlementService is concerned.
        var expiredCount = await _dbContext.SubscriptionRecords
            .Where(s => s.Status == SubscriptionStatus.Active && s.PeriodEnd != null && s.PeriodEnd <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, SubscriptionStatus.Expired), cancellationToken);

        if (expiredCount > 0)
        {
            _logger.LogInformation("ExpireTrialsAndSubscriptions expired {ExpiredCount} subscription(s) past their period end.", expiredCount);
        }

        var lapsedTrialUserIds = await _dbContext.Users.Where(u => u.TrialEndsAt <= now).Select(u => u.Id).ToListAsync(cancellationToken);
        if (lapsedTrialUserIds.Count == 0)
        {
            return;
        }

        // A lapsed trial only actually pauses anything for a user who has not since
        // gone Pro for real, checked fresh here (after the expiry pass above)
        // rather than trusting a snapshot from earlier in this same run.
        var stillActiveProUserIds = await _dbContext.SubscriptionRecords
            .Where(s => lapsedTrialUserIds.Contains(s.UserId) && s.Status == SubscriptionStatus.Active && (s.PeriodEnd == null || s.PeriodEnd > now))
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var lapsedUserIds = lapsedTrialUserIds.Except(stillActiveProUserIds).ToList();
        if (lapsedUserIds.Count == 0)
        {
            return;
        }

        var pausedCount = await _dbContext.RecurringItems
            .Where(r => lapsedUserIds.Contains(r.UserId) && r.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsActive, false), cancellationToken);

        if (pausedCount > 0)
        {
            _logger.LogInformation(
                "ExpireTrialsAndSubscriptions paused {PausedCount} recurring item(s) across {UserCount} lapsed-trial account(s) with no active Pro subscription.",
                pausedCount, lapsedUserIds.Count);
        }
    }

    public async Task PurgePendingDeletionsAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _clock.UtcNow.AddDays(-_appDefaults.PendingDeletionGraceDays);
        var dueUsers = await _dbContext.Users
            .Where(u => u.Status == UserStatus.PendingDeletion && u.PendingDeletionRequestedAt != null && u.PendingDeletionRequestedAt <= cutoff)
            .ToListAsync(cancellationToken);

        foreach (var user in dueUsers)
        {
            await PurgeOneUserAsync(user, cancellationToken);
        }
    }

    // Every child table (repayments, redemptions, splits, tags, category months and
    // allocations) is cascade-deleted by Postgres itself the moment its parent row
    // here goes, so only the top-level, directly user-owned tables need deleting by
    // hand. Order does not matter to Postgres for these, none of them reference each
    // other, only the user they all point back to.
    private async Task PurgeOneUserAsync(User user, CancellationToken cancellationToken)
    {
        var userId = user.Id;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        await _dbContext.RefreshTokens.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.PasswordResetTokens.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DataExportRequests.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.IdempotencyRecords.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Notifications.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.AlertPreferences.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.RecurringItems.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.InvestmentLogs.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.LoansOut.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DebtsIn.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Promises.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Reallocations.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.DeficitResolutions.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.LedgerEntries.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Expenses.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Tags.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Incomes.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.BudgetMonths.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.AllocationConfigVersions.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);
        await _dbContext.Categories.Where(x => x.UserId == userId).ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // AuditEvents are deliberately left alone, an audit trail of what happened
        // (including who asked to be deleted, and when) should outlive the account
        // it is about, the same reasoning that keeps audit logs around in any real
        // system after the account they describe is gone.
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogError(
                "PurgePendingDeletions removed {UserId}'s data but could not remove the login itself: {Errors}",
                userId, string.Join(", ", result.Errors.Select(e => e.Description)));
            return;
        }

        _logger.LogInformation("PurgePendingDeletions removed account {UserId} for good.", userId);
    }

    public async Task<int> RunLedgerIntegrityCheckAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.BudgetMonths
            .SelectMany(m => m.CategoryMonths, (m, cm) => new { m.UserId, cm.CategoryId, m.Year, m.Month, m.Status })
            .ToListAsync(cancellationToken);

        // Only a category whose most recently touched month is Closed should net to
        // zero, one still Open is legitimately holding a real balance in progress.
        var settledCategories = rows
            .GroupBy(r => new { r.UserId, r.CategoryId })
            .Select(g => g.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month).First())
            .Where(r => r.Status == BudgetMonthStatus.Closed)
            .ToList();

        var driftCount = 0;
        foreach (var row in settledCategories)
        {
            var balance = await _ledgerService.GetAccountBalanceAsync(row.UserId, AccountRef.Category(row.CategoryId), cancellationToken);
            if (balance != Money.Zero)
            {
                driftCount++;
                _logger.LogError(
                    "LedgerIntegrityCheck drift: user {UserId} category {CategoryId} should net to zero after its last closed month "
                        + "({Year}-{Month:D2}) but the ledger shows a balance of {Balance}.",
                    row.UserId, row.CategoryId, row.Year, row.Month, balance.Amount);
            }
        }

        _logger.LogInformation(
            "LedgerIntegrityCheck checked {CheckedCount} settled categor(y/ies), found {DriftCount} with drift.",
            settledCategories.Count, driftCount);

        return driftCount;
    }
}
