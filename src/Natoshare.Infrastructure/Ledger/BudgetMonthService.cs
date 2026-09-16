using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Notifications;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// Resolves and opens a user's months. A category's AllocatedAmount is not filled in
// automatically just because a month starts, it only grows as the user actually logs
// real Allocatable income (see IncomeService). Opening a month here only decides
// WHICH categories and percentages apply, using whichever AllocationConfigVersion is
// active for that month, and freezes that choice forever. If the previous month was
// actually closed, this is also where its leftover savings and deficit get pulled
// forward.
public class BudgetMonthService : IBudgetMonthService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;

    public BudgetMonthService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
    }

    public async Task<MonthSnapshot> GetSnapshotAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == year && m.Month == month, cancellationToken);

        if (existing is not null)
        {
            var categoryLookup = await CategoryLookupAsync(existing.CategoryMonths.Select(cm => cm.CategoryId), cancellationToken);
            return new MonthSnapshot
            {
                Year = year,
                Month = month,
                Status = existing.Status.ToString(),
                BudgetMonthId = existing.Id,
                FixedIncomeSnapshot = existing.FixedIncomeSnapshot,
                Categories = existing.CategoryMonths.Select(cm => ToSnapshot(cm, categoryLookup)).ToList(),
            };
        }

        // Nothing has happened in this month yet. Work out what it WOULD look like
        // without saving anything, so a balances screen still has real numbers to
        // show before the user's first income or expense.
        var version = await ResolveActiveVersionAsync(userId, year, month, cancellationToken);
        if (version is null)
        {
            return new MonthSnapshot { Year = year, Month = month, Status = "Open", BudgetMonthId = null, FixedIncomeSnapshot = Money.Zero };
        }

        var categoryIds = version.Allocations.Select(a => a.CategoryId).ToList();
        var previewCategories = await CategoryLookupAsync(categoryIds, cancellationToken);

        // A closed previous month's leftover savings and deficit are real,
        // already-realised numbers, not a guess, so the preview shows them too
        // instead of pretending the month starts from nothing (this is exactly
        // what actually gets applied the moment the month opens for real).
        var previousMonth = new DateOnly(year, month, 1).AddMonths(-1);
        var previousCategoryMonths = await PreviousClosedCategoryMonthsAsync(userId, previousMonth, cancellationToken);
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var sinkingFundEnabled = SinkingFundEnabledFor(user);

        return new MonthSnapshot
        {
            Year = year,
            Month = month,
            Status = "Open",
            BudgetMonthId = null,
            FixedIncomeSnapshot = version.FixedIncomeAmount,
            Categories = version.Allocations.Select(a =>
            {
                previewCategories.TryGetValue(a.CategoryId, out var category);
                previousCategoryMonths.TryGetValue(a.CategoryId, out var previous);
                return new MonthCategorySnapshot
                {
                    CategoryId = a.CategoryId,
                    Name = category?.Name ?? "(deleted category)",
                    Kind = category?.Kind.ToString() ?? "Standard",
                    Allocated = Money.Zero,
                    CarriedInSavings = sinkingFundEnabled ? previous?.CarriedOutSavings ?? Money.Zero : Money.Zero,
                    CarriedInDeficit = previous?.CarriedOutDeficit ?? Money.Zero,
                    Spent = Money.Zero,
                    Covered = Money.Zero,
                    ExternalTransferAmount = null,
                };
            }).ToList(),
        };
    }

    public async Task<Guid> EnsureOpenAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.BudgetMonths
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == year && m.Month == month, cancellationToken);

        if (existing is not null)
        {
            if (existing.Status == BudgetMonthStatus.Closed)
            {
                throw new ConflictException("This month is closed, nothing can be logged against it any more.");
            }

            return existing.Id;
        }

        var version = await ResolveActiveVersionAsync(userId, year, month, cancellationToken)
            ?? throw new ConflictException("Set up your income and categories before logging anything.");

        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var previousMonth = new DateOnly(year, month, 1).AddMonths(-1);
        var previousCategoryMonths = await PreviousClosedCategoryMonthsAsync(userId, previousMonth, cancellationToken);
        var sinkingFundEnabled = SinkingFundEnabledFor(user);

        var budgetMonth = new BudgetMonth
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Year = year,
            Month = month,
            Status = BudgetMonthStatus.Open,
            AllocationConfigVersionId = version.Id,
            FixedIncomeSnapshot = version.FixedIncomeAmount,
            OpenedAt = _clock.UtcNow,
        };

        _dbContext.BudgetMonths.Add(budgetMonth);

        var categoryMonths = new List<CategoryMonth>();
        foreach (var allocation in version.Allocations)
        {
            previousCategoryMonths.TryGetValue(allocation.CategoryId, out var previous);

            var categoryMonth = new CategoryMonth
            {
                Id = Guid.CreateVersion7(),
                BudgetMonthId = budgetMonth.Id,
                CategoryId = allocation.CategoryId,
                AllocatedAmount = Money.Zero,
                // Free plan accounts do not carry savings forward (a real product
                // rule, see 00-plan.md), but plan/entitlement checking is Phase 9
                // work, so this always applies for now, same as every other
                // plan-gated check in the app until then.
                CarriedInSavings = sinkingFundEnabled ? previous?.CarriedOutSavings ?? Money.Zero : Money.Zero,
                CarriedInDeficit = previous?.CarriedOutDeficit ?? Money.Zero,
            };

            categoryMonths.Add(categoryMonth);
            _dbContext.CategoryMonths.Add(categoryMonth);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // These are real, already-realised numbers from a closed (immutable) month,
        // not a guess, so unlike the fixed income snapshot they are safe to post to
        // the ledger straight away instead of waiting for the user to do anything.
        foreach (var categoryMonth in categoryMonths)
        {
            if (categoryMonth.CarriedInSavings > Money.Zero)
            {
                await _ledgerService.PostAsync(
                    userId, budgetMonth.Id, AccountRef.Category(categoryMonth.CategoryId), LedgerEntryType.Rollover,
                    categoryMonth.CarriedInSavings, LedgerDirection.Credit, SourceTxnType.MonthOpen, budgetMonth.Id,
                    "Savings carried in from last month", cancellationToken);
                await _ledgerService.PostAsync(
                    userId, budgetMonth.Id, AccountRef.CategorySavings(categoryMonth.CategoryId), LedgerEntryType.Rollover,
                    categoryMonth.CarriedInSavings, LedgerDirection.Debit, SourceTxnType.MonthOpen, budgetMonth.Id,
                    "Savings carried in from last month", cancellationToken);
            }

            if (categoryMonth.CarriedInDeficit > Money.Zero)
            {
                await _ledgerService.PostAsync(
                    userId, budgetMonth.Id, AccountRef.Category(categoryMonth.CategoryId), LedgerEntryType.DeficitCarryForward,
                    categoryMonth.CarriedInDeficit, LedgerDirection.Debit, SourceTxnType.MonthOpen, budgetMonth.Id,
                    "Deficit carried in from last month", cancellationToken);

                await NotifyCarriedDeficitAppliedAsync(user, categoryMonth, cancellationToken);
            }
        }

        return budgetMonth.Id;
    }

    private async Task<Dictionary<Guid, CategoryMonth>> PreviousClosedCategoryMonthsAsync(
        Guid userId, DateOnly previousMonth, CancellationToken cancellationToken)
    {
        var previous = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.Year == previousMonth.Year && m.Month == previousMonth.Month
                    && m.Status == BudgetMonthStatus.Closed,
                cancellationToken);

        return previous?.CategoryMonths.ToDictionary(cm => cm.CategoryId) ?? [];
    }

    // Stubbed the same way plan limits are stubbed everywhere else in the app
    // (see CategoryService), until Phase 9 builds real plans this always says yes.
    private static bool SinkingFundEnabledFor(User user) => true;

    private async Task NotifyCarriedDeficitAppliedAsync(User user, CategoryMonth categoryMonth, CancellationToken cancellationToken)
    {
        var preference = await _dbContext.AlertPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Kind == NotificationKind.CarriedDeficitApplied, cancellationToken);

        if (preference is { Enabled: false })
        {
            return;
        }

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == categoryMonth.CategoryId, cancellationToken);

        _dbContext.Notifications.Add(new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Kind = NotificationKind.CarriedDeficitApplied,
            Title = $"{category?.Name ?? "A category"} starts the month short",
            Body = $"{user.CurrencySymbol}{categoryMonth.CarriedInDeficit.Amount:N2} carried in from last month's deficit, "
                + "reducing what is funded this month.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = "CategoryMonth",
            RelatedEntityId = categoryMonth.Id.ToString(),
            CreatedAt = _clock.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AllocationConfigVersion?> ResolveActiveVersionAsync(Guid userId, int year, int month, CancellationToken cancellationToken)
    {
        var versions = await _dbContext.AllocationConfigVersions
            .Include(v => v.Allocations)
            .Where(v => v.UserId == userId)
            .ToListAsync(cancellationToken);

        return AllocationResolver.ResolveActiveVersion(versions, new DateOnly(year, month, 1));
    }

    private async Task<Dictionary<Guid, Category>> CategoryLookupAsync(IEnumerable<Guid> categoryIds, CancellationToken cancellationToken)
    {
        var ids = categoryIds.ToList();
        var categories = await _dbContext.Categories.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        return categories.ToDictionary(c => c.Id);
    }

    private static MonthCategorySnapshot ToSnapshot(CategoryMonth cm, Dictionary<Guid, Category> categories)
    {
        categories.TryGetValue(cm.CategoryId, out var category);
        return new MonthCategorySnapshot
        {
            CategoryId = cm.CategoryId,
            Name = category?.Name ?? "(deleted category)",
            Kind = category?.Kind.ToString() ?? "Standard",
            Allocated = cm.AllocatedAmount,
            CarriedInSavings = cm.CarriedInSavings,
            CarriedInDeficit = cm.CarriedInDeficit,
            Spent = cm.SpentAmount,
            Covered = cm.CoveredAmount,
            ExternalTransferAmount = cm.ExternalTransferAmount,
        };
    }
}
