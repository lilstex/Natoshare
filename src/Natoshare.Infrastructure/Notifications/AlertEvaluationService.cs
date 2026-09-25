using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.Notifications;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Notifications;
using Natoshare.Domain.PeopleAndMoney;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Notifications;

// Works out whether a category's current numbers deserve an alert. Every alert here
// is keyed to one CategoryMonth (one category's occurrence in one specific month), so
// dedup and "has this ever fired" checks never leak across months.
public class AlertEvaluationService : IAlertEvaluationService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;

    public AlertEvaluationService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
    }

    public async Task EvaluateCategoryAsync(Guid userId, Guid budgetMonthId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        var budgetMonth = await _dbContext.BudgetMonths.FirstOrDefaultAsync(m => m.Id == budgetMonthId, cancellationToken);
        var categoryMonth = await _dbContext.CategoryMonths
            .FirstOrDefaultAsync(cm => cm.BudgetMonthId == budgetMonthId && cm.CategoryId == categoryId, cancellationToken);
        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);

        if (user is null || budgetMonth is null || categoryMonth is null || category is null)
        {
            return;
        }

        var preferences = await _dbContext.AlertPreferences.Where(p => p.UserId == userId).ToListAsync(cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);

        await EvaluateOneCategoryAsync(user, budgetMonth, categoryMonth, category, preferences, today, cancellationToken);
    }

    // Run once an hour by a recurring job, this is what catches an alert that only
    // time passing can trigger (pacing and safe-to-spend both depend on today's
    // date, not on anything new having been logged).
    public async Task EvaluateAllOpenMonthsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users.Select(u => new { u.Id, u.TimeZoneId }).ToListAsync(cancellationToken);

        foreach (var userRow in users)
        {
            var today = UserTime.TodayFor(userRow.TimeZoneId, _clock.UtcNow);

            var budgetMonth = await _dbContext.BudgetMonths
                .Include(m => m.CategoryMonths)
                .FirstOrDefaultAsync(m => m.UserId == userRow.Id && m.Year == today.Year && m.Month == today.Month, cancellationToken);

            if (budgetMonth is null)
            {
                continue;
            }

            var user = await _dbContext.Users.FirstAsync(u => u.Id == userRow.Id, cancellationToken);
            var categories = await _dbContext.Categories.Where(c => c.UserId == userRow.Id).ToDictionaryAsync(c => c.Id, cancellationToken);
            var preferences = await _dbContext.AlertPreferences.Where(p => p.UserId == userRow.Id).ToListAsync(cancellationToken);

            foreach (var categoryMonth in budgetMonth.CategoryMonths)
            {
                if (categories.TryGetValue(categoryMonth.CategoryId, out var category))
                {
                    await EvaluateOneCategoryAsync(user, budgetMonth, categoryMonth, category, preferences, today, cancellationToken);
                }
            }
        }
    }

    private async Task EvaluateOneCategoryAsync(
        User user, BudgetMonth budgetMonth, CategoryMonth categoryMonth, Category category,
        List<AlertPreference> preferences, DateOnly today, CancellationToken cancellationToken)
    {
        // Deficit-related alerts make sense for any category kind, a FixedAccount
        // like Rent can go into deficit too.
        if (categoryMonth.Deficit() > Money.Zero)
        {
            await MaybeCreateOverspendAsync(user, categoryMonth, category, preferences, cancellationToken);
            await MaybeCreateDeficitAsync(user, categoryMonth, category, preferences, today, cancellationToken);
        }

        // Pacing only makes sense for day-to-day Standard spending, not a lump-sum
        // FixedAccount category like Rent or Investment.
        if (category.Kind == CategoryKind.Standard)
        {
            var pace = PacingCalculator.Compute(categoryMonth, budgetMonth.Year, budgetMonth.Month, today);
            await MaybeCreateOverPaceAsync(user, budgetMonth, categoryMonth, category, preferences, pace, today, cancellationToken);
            await MaybeCreateSafeToSpendLowAsync(user, budgetMonth, categoryMonth, category, preferences, pace, today, cancellationToken);
        }
    }

    // Fires exactly once per month, the first time a category actually crosses into
    // deficit, "you just went over" is only news the first time it happens.
    private async Task MaybeCreateOverspendAsync(
        User user, CategoryMonth categoryMonth, Category category, List<AlertPreference> preferences, CancellationToken cancellationToken)
    {
        if (IsDisabled(preferences, NotificationKind.OverspendCategory))
        {
            return;
        }

        if (await HasEverFiredAsync(user.Id, NotificationKind.OverspendCategory, categoryMonth.Id, cancellationToken))
        {
            return;
        }

        await CreateAsync(
            user.Id, NotificationKind.OverspendCategory, NotificationSeverity.Warning,
            $"{category.Name} is over budget",
            $"{category.Name} has gone {FormatAmount(user, categoryMonth.Deficit())} over its funded amount for this month.",
            categoryMonth.Id, cancellationToken);
    }

    // Fires once a day for as long as the deficit stays unresolved, and gets louder
    // the longer it drags on.
    private async Task MaybeCreateDeficitAsync(
        User user, CategoryMonth categoryMonth, Category category, List<AlertPreference> preferences, DateOnly today, CancellationToken cancellationToken)
    {
        if (IsDisabled(preferences, NotificationKind.CategoryInDeficit))
        {
            return;
        }

        if (await AlreadyFiredTodayAsync(user.Id, NotificationKind.CategoryInDeficit, categoryMonth.Id, today, user.TimeZoneId, cancellationToken))
        {
            return;
        }

        var firstFiredOn = await FirstFiredOnAsync(user.Id, NotificationKind.CategoryInDeficit, categoryMonth.Id, user.TimeZoneId, cancellationToken);
        var daysInDeficit = firstFiredOn is null ? 0 : today.DayNumber - firstFiredOn.Value.DayNumber;
        var severity = daysInDeficit >= 3 ? NotificationSeverity.Critical : NotificationSeverity.Warning;

        await CreateAsync(
            user.Id, NotificationKind.CategoryInDeficit, severity,
            $"{category.Name} is still in deficit",
            daysInDeficit > 0
                ? $"{category.Name} has been {FormatAmount(user, categoryMonth.Deficit())} in deficit for {daysInDeficit} day(s) now."
                : $"{category.Name} is {FormatAmount(user, categoryMonth.Deficit())} in deficit.",
            categoryMonth.Id, cancellationToken);
    }

    private async Task MaybeCreateOverPaceAsync(
        User user, BudgetMonth budgetMonth, CategoryMonth categoryMonth, Category category, List<AlertPreference> preferences,
        PacingCalculator.Result pace, DateOnly today, CancellationToken cancellationToken)
    {
        if (IsDisabled(preferences, NotificationKind.OverPaceCategory))
        {
            return;
        }

        var funded = categoryMonth.Funded().Amount;
        if (funded <= 0m)
        {
            return;
        }

        var thresholdPercent = ThresholdFor(preferences, NotificationKind.OverPaceCategory, defaultValue: 110m);
        if (pace.Projected <= funded * (thresholdPercent / 100m))
        {
            return;
        }

        if (await AlreadyFiredTodayAsync(user.Id, NotificationKind.OverPaceCategory, categoryMonth.Id, today, user.TimeZoneId, cancellationToken))
        {
            return;
        }

        await CreateAsync(
            user.Id, NotificationKind.OverPaceCategory, NotificationSeverity.Warning,
            $"{category.Name} is spending faster than planned",
            $"At this pace, {category.Name} is projected to reach {FormatAmount(user, new Money(pace.Projected))} by month end, "
                + $"against {FormatAmount(user, new Money(funded))} funded.",
            categoryMonth.Id, cancellationToken);
    }

    private async Task MaybeCreateSafeToSpendLowAsync(
        User user, BudgetMonth budgetMonth, CategoryMonth categoryMonth, Category category, List<AlertPreference> preferences,
        PacingCalculator.Result pace, DateOnly today, CancellationToken cancellationToken)
    {
        if (IsDisabled(preferences, NotificationKind.SafeToSpendLow))
        {
            return;
        }

        // The deficit alert already covers "this needs attention", no need to also
        // say "running low" once it has actually run out.
        if (categoryMonth.Deficit() > Money.Zero)
        {
            return;
        }

        var daysInMonth = DateTime.DaysInMonth(budgetMonth.Year, budgetMonth.Month);
        var flatDailyRate = categoryMonth.Funded().Amount / daysInMonth;
        if (flatDailyRate <= 0m)
        {
            return;
        }

        var thresholdPercent = ThresholdFor(preferences, NotificationKind.SafeToSpendLow, defaultValue: 20m);
        if (pace.SafeToSpendDaily >= flatDailyRate * (thresholdPercent / 100m))
        {
            return;
        }

        if (await AlreadyFiredTodayAsync(user.Id, NotificationKind.SafeToSpendLow, categoryMonth.Id, today, user.TimeZoneId, cancellationToken))
        {
            return;
        }

        await CreateAsync(
            user.Id, NotificationKind.SafeToSpendLow, NotificationSeverity.Warning,
            $"{category.Name} is running low",
            $"Only {FormatAmount(user, new Money(pace.SafeToSpendDaily))} a day is safe to spend on {category.Name} for the rest of the month.",
            categoryMonth.Id, cancellationToken);
    }

    // Run once a day by a recurring job: debts and loans coming due or already
    // overdue, and promises the user can now afford to redeem straight from the
    // Flexible Pool.
    public async Task EvaluateObligationsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users.Select(u => new { u.Id, u.TimeZoneId }).ToListAsync(cancellationToken);

        foreach (var userRow in users)
        {
            var today = UserTime.TodayFor(userRow.TimeZoneId, _clock.UtcNow);
            var user = await _dbContext.Users.FirstAsync(u => u.Id == userRow.Id, cancellationToken);
            var preferences = await _dbContext.AlertPreferences.Where(p => p.UserId == userRow.Id).ToListAsync(cancellationToken);

            var debts = await _dbContext.DebtsIn
                .Where(d => d.UserId == userRow.Id && d.Status != DebtInStatus.Repaid && d.DueOn != null)
                .ToListAsync(cancellationToken);
            foreach (var debt in debts)
            {
                await EvaluateDueDateAsync(
                    user, debt.Id, "DebtIn", debt.DueOn!.Value, today, preferences,
                    NotificationKind.DebtDueSoon, NotificationKind.DebtOverdue,
                    $"{debt.LenderName} needs to be paid back soon", $"{debt.LenderName} is now overdue to be paid back", cancellationToken);
            }

            var loans = await _dbContext.LoansOut
                .Where(l => l.UserId == userRow.Id && l.Status != LoanOutStatus.Repaid && l.Status != LoanOutStatus.WrittenOff && l.ExpectedReturnOn != null)
                .ToListAsync(cancellationToken);
            foreach (var loan in loans)
            {
                await EvaluateDueDateAsync(
                    user, loan.Id, "LoanOut", loan.ExpectedReturnOn!.Value, today, preferences,
                    NotificationKind.LoanReturnDueSoon, NotificationKind.LoanOverdue,
                    $"{loan.BorrowerName} was expected to return this soon", $"{loan.BorrowerName} is now overdue to return this", cancellationToken);
            }

            if (!IsDisabled(preferences, NotificationKind.PromiseReminder))
            {
                var pool = await _ledgerService.GetAccountBalanceAsync(userRow.Id, AccountRef.FlexiblePool(), cancellationToken);
                var openPromises = await _dbContext.Promises
                    .Include(p => p.Redemptions)
                    .Where(p => p.UserId == userRow.Id && (p.Status == PromiseStatus.Open || p.Status == PromiseStatus.PartiallyRedeemed))
                    .ToListAsync(cancellationToken);

                foreach (var promise in openPromises)
                {
                    var outstanding = Math.Max(0m, promise.Amount.Amount - promise.Redemptions.Sum(r => r.Amount.Amount));
                    if (pool.Amount < outstanding)
                    {
                        continue;
                    }

                    if (await AlreadyFiredTodayAsync(userRow.Id, NotificationKind.PromiseReminder, promise.Id, today, userRow.TimeZoneId, cancellationToken))
                    {
                        continue;
                    }

                    await CreateAsync(
                        userRow.Id, NotificationKind.PromiseReminder, NotificationSeverity.Info,
                        $"You can now redeem your promise to {promise.PersonName}",
                        $"The Flexible Pool now holds enough to redeem the {FormatAmount(user, new Money(outstanding))} still owed to {promise.PersonName}.",
                        promise.Id, cancellationToken, relatedEntityType: "Promise");
                }
            }
        }
    }

    private async Task EvaluateDueDateAsync(
        User user, Guid entityId, string entityType, DateOnly dueDate, DateOnly today, List<AlertPreference> preferences,
        NotificationKind dueSoonKind, NotificationKind overdueKind, string dueSoonTitle, string overdueTitle, CancellationToken cancellationToken)
    {
        var daysUntilDue = dueDate.DayNumber - today.DayNumber;

        if (daysUntilDue < 0)
        {
            if (IsDisabled(preferences, overdueKind))
            {
                return;
            }

            if (await AlreadyFiredTodayAsync(user.Id, overdueKind, entityId, today, user.TimeZoneId, cancellationToken))
            {
                return;
            }

            await CreateAsync(user.Id, overdueKind, NotificationSeverity.Critical, overdueTitle, overdueTitle, entityId, cancellationToken, entityType);
            return;
        }

        if (IsDisabled(preferences, dueSoonKind))
        {
            return;
        }

        var leadDays = preferences.FirstOrDefault(p => p.Kind == dueSoonKind)?.LeadDays ?? 3;
        if (daysUntilDue > leadDays)
        {
            return;
        }

        if (await AlreadyFiredTodayAsync(user.Id, dueSoonKind, entityId, today, user.TimeZoneId, cancellationToken))
        {
            return;
        }

        await CreateAsync(user.Id, dueSoonKind, NotificationSeverity.Warning, dueSoonTitle, dueSoonTitle, entityId, cancellationToken, entityType);
    }

    private static bool IsDisabled(List<AlertPreference> preferences, NotificationKind kind) =>
        preferences.FirstOrDefault(p => p.Kind == kind) is { Enabled: false };

    private static decimal ThresholdFor(List<AlertPreference> preferences, NotificationKind kind, decimal defaultValue) =>
        preferences.FirstOrDefault(p => p.Kind == kind)?.ThresholdPercent ?? defaultValue;

    private async Task<bool> HasEverFiredAsync(Guid userId, NotificationKind kind, Guid categoryMonthId, CancellationToken cancellationToken)
    {
        var relatedEntityId = categoryMonthId.ToString();
        return await _dbContext.Notifications
            .AnyAsync(n => n.UserId == userId && n.Kind == kind && n.RelatedEntityId == relatedEntityId, cancellationToken);
    }

    private async Task<bool> AlreadyFiredTodayAsync(
        Guid userId, NotificationKind kind, Guid categoryMonthId, DateOnly today, string timeZoneId, CancellationToken cancellationToken)
    {
        var relatedEntityId = categoryMonthId.ToString();
        var mostRecent = await _dbContext.Notifications
            .Where(n => n.UserId == userId && n.Kind == kind && n.RelatedEntityId == relatedEntityId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => (DateTimeOffset?)n.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return mostRecent is not null && UserTime.TodayFor(timeZoneId, mostRecent.Value) == today;
    }

    private async Task<DateOnly?> FirstFiredOnAsync(
        Guid userId, NotificationKind kind, Guid categoryMonthId, string timeZoneId, CancellationToken cancellationToken)
    {
        var relatedEntityId = categoryMonthId.ToString();
        var earliest = await _dbContext.Notifications
            .Where(n => n.UserId == userId && n.Kind == kind && n.RelatedEntityId == relatedEntityId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => (DateTimeOffset?)n.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return earliest is null ? null : UserTime.TodayFor(timeZoneId, earliest.Value);
    }

    private async Task CreateAsync(
        Guid userId, NotificationKind kind, NotificationSeverity severity, string title, string body, Guid relatedEntityId,
        CancellationToken cancellationToken, string relatedEntityType = "CategoryMonth")
    {
        _dbContext.Notifications.Add(new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            Title = title,
            Body = body,
            Severity = severity,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId.ToString(),
            CreatedAt = _clock.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string FormatAmount(User user, Money amount) => $"{user.CurrencySymbol}{amount.Amount:N2}";
}
