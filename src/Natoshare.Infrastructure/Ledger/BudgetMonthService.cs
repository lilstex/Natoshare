using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// Resolves and opens a user's months. A category's AllocatedAmount is not filled in
// automatically just because a month starts, it only grows as the user actually logs
// real Allocatable income (see IncomeService). Opening a month here only decides
// WHICH categories and percentages apply, using whichever AllocationConfigVersion is
// active for that month, and freezes that choice forever.
public class BudgetMonthService : IBudgetMonthService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;

    public BudgetMonthService(NatoshareDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
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
                return new MonthCategorySnapshot
                {
                    CategoryId = a.CategoryId,
                    Name = category?.Name ?? "(deleted category)",
                    Kind = category?.Kind.ToString() ?? "Standard",
                    Allocated = Money.Zero,
                    CarriedInSavings = Money.Zero,
                    CarriedInDeficit = Money.Zero,
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
            return existing.Id;
        }

        var version = await ResolveActiveVersionAsync(userId, year, month, cancellationToken)
            ?? throw new ConflictException("Set up your income and categories before logging anything.");

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

        foreach (var allocation in version.Allocations)
        {
            _dbContext.CategoryMonths.Add(new CategoryMonth
            {
                Id = Guid.CreateVersion7(),
                BudgetMonthId = budgetMonth.Id,
                CategoryId = allocation.CategoryId,
                AllocatedAmount = Money.Zero,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return budgetMonth.Id;
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
