using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Transactions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// Money coming in. Allocatable income is what actually funds a category, split the
// moment it is logged using that month's active percentages, Flexible income just
// tops up the shared Flexible Pool.
public class IncomeService : IIncomeService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;
    private readonly IEntitlementService _entitlementService;

    public IncomeService(
        NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService,
        IEntitlementService entitlementService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
        _entitlementService = entitlementService;
    }

    public async Task<IReadOnlyList<IncomeDto>> ListAsync(
        Guid userId, string? type, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Incomes.Include(i => i.Splits).Where(i => i.UserId == userId);

        if (type is not null)
        {
            query = query.Where(i => i.Type.ToString() == type);
        }

        from = await ClampToHistoryWindowAsync(userId, from, cancellationToken);
        if (from is not null)
        {
            query = query.Where(i => i.OccurredOn >= from);
        }

        if (to is not null)
        {
            query = query.Where(i => i.OccurredOn <= to);
        }

        var incomes = await query
            .OrderByDescending(i => i.OccurredOn).ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var categories = await CategoryLookupAsync(incomes.SelectMany(i => i.Splits.Select(s => s.CategoryId)), cancellationToken);
        return incomes.Select(i => ToDto(i, categories)).ToList();
    }

    public async Task<IncomeDto> GetAsync(Guid userId, Guid incomeId, CancellationToken cancellationToken = default)
    {
        var income = await LoadOwnedAsync(userId, incomeId, cancellationToken);
        var categories = await CategoryLookupAsync(income.Splits.Select(s => s.CategoryId), cancellationToken);
        return ToDto(income, categories);
    }

    public async Task<IncomeDto> CreateAsync(Guid userId, LogIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var timeZoneId = await _dbContext.Users.Where(u => u.Id == userId).Select(u => u.TimeZoneId).FirstAsync(cancellationToken);
        var occurredOn = request.OccurredOn ?? UserTime.TodayFor(timeZoneId, _clock.UtcNow);
        var budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, occurredOn.Year, occurredOn.Month, cancellationToken);

        var amount = new Money(request.Amount);
        var incomeType = request.Type == "Allocatable" ? IncomeType.Allocatable : IncomeType.Flexible;

        var income = new Income
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Type = incomeType,
            TotalAmount = amount,
            Description = request.Description,
            OccurredOn = occurredOn,
            BudgetMonthId = budgetMonthId,
            CreatedAt = _clock.UtcNow,
        };

        if (incomeType == IncomeType.Allocatable)
        {
            await SplitAllocatableIncomeAsync(income, budgetMonthId, amount, cancellationToken);
        }

        _dbContext.Incomes.Add(income);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (incomeType == IncomeType.Allocatable)
        {
            foreach (var split in income.Splits.Where(s => s.Amount != Money.Zero))
            {
                await _ledgerService.PostAsync(
                    userId, budgetMonthId, AccountRef.Category(split.CategoryId), LedgerEntryType.Income,
                    split.Amount, LedgerDirection.Credit, SourceTxnType.Income, income.Id, request.Description, cancellationToken);
            }
        }
        else
        {
            await _ledgerService.PostAsync(
                userId, budgetMonthId, AccountRef.FlexiblePool(), LedgerEntryType.Income,
                amount, LedgerDirection.Credit, SourceTxnType.Income, income.Id, request.Description, cancellationToken);
        }

        var categories = await CategoryLookupAsync(income.Splits.Select(s => s.CategoryId), cancellationToken);
        return ToDto(income, categories);
    }

    public async Task<IncomeDto> UpdateAsync(Guid userId, Guid incomeId, UpdateIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var income = await LoadOwnedAsync(userId, incomeId, cancellationToken);
        if (income.Status == TransactionStatus.Reversed)
        {
            throw new ConflictException("This income has already been removed.");
        }

        await EnsureMonthNotClosedAsync(income.BudgetMonthId, cancellationToken);

        var newOccurredOn = request.OccurredOn ?? income.OccurredOn;
        if (newOccurredOn.Year != income.OccurredOn.Year || newOccurredOn.Month != income.OccurredOn.Month)
        {
            throw new ConflictException("Moving an income to a different month is not supported yet, delete it and log a fresh one instead.");
        }

        var newAmount = request.Amount.HasValue ? new Money(request.Amount.Value) : income.TotalAmount;

        // Undo what this income originally did, then split the new amount fresh. The
        // row itself keeps the same id, only the ledger gets new entries.
        await _ledgerService.ReverseAsync(SourceTxnType.Income, income.Id, "Edited", cancellationToken);

        if (income.Type == IncomeType.Allocatable)
        {
            await UndoSplitsAsync(income, cancellationToken);
            income.Splits.Clear();
            await SplitAllocatableIncomeAsync(income, income.BudgetMonthId, newAmount, cancellationToken);
        }

        income.TotalAmount = newAmount;
        income.Description = request.Description ?? income.Description;
        income.OccurredOn = newOccurredOn;

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (income.Type == IncomeType.Allocatable)
        {
            foreach (var split in income.Splits.Where(s => s.Amount != Money.Zero))
            {
                await _ledgerService.PostAsync(
                    userId, income.BudgetMonthId, AccountRef.Category(split.CategoryId), LedgerEntryType.Income,
                    split.Amount, LedgerDirection.Credit, SourceTxnType.Income, income.Id, income.Description, cancellationToken);
            }
        }
        else
        {
            await _ledgerService.PostAsync(
                userId, income.BudgetMonthId, AccountRef.FlexiblePool(), LedgerEntryType.Income,
                newAmount, LedgerDirection.Credit, SourceTxnType.Income, income.Id, income.Description, cancellationToken);
        }

        var categories = await CategoryLookupAsync(income.Splits.Select(s => s.CategoryId), cancellationToken);
        return ToDto(income, categories);
    }

    public async Task DeleteAsync(Guid userId, Guid incomeId, CancellationToken cancellationToken = default)
    {
        var income = await LoadOwnedAsync(userId, incomeId, cancellationToken);
        if (income.Status == TransactionStatus.Reversed)
        {
            return;
        }

        await EnsureMonthNotClosedAsync(income.BudgetMonthId, cancellationToken);
        await _ledgerService.ReverseAsync(SourceTxnType.Income, income.Id, "Deleted", cancellationToken);

        if (income.Type == IncomeType.Allocatable)
        {
            await UndoSplitsAsync(income, cancellationToken);
        }

        income.Status = TransactionStatus.Reversed;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Splits an Allocatable amount across this month's categories, using the exact
    // percentages that were active when the month opened, and adds each share onto
    // the category's running AllocatedAmount for the month.
    private async Task SplitAllocatableIncomeAsync(Income income, Guid budgetMonthId, Money amount, CancellationToken cancellationToken)
    {
        var budgetMonth = await _dbContext.BudgetMonths.FirstAsync(m => m.Id == budgetMonthId, cancellationToken);
        var version = await _dbContext.AllocationConfigVersions
            .Include(v => v.Allocations)
            .FirstAsync(v => v.Id == budgetMonth.AllocationConfigVersionId, cancellationToken);

        if (version.Allocations.Count == 0)
        {
            throw new ConflictException("There are no categories set up for this month.");
        }

        var categoryMonths = await _dbContext.CategoryMonths
            .Where(cm => cm.BudgetMonthId == budgetMonthId)
            .ToDictionaryAsync(cm => cm.CategoryId, cancellationToken);

        var percentages = version.Allocations.Select(a => a.Percentage).ToArray();
        var amounts = amount.Allocate(percentages);

        for (var i = 0; i < version.Allocations.Count; i++)
        {
            var categoryId = version.Allocations[i].CategoryId;
            var splitAmount = amounts[i];

            income.Splits.Add(new IncomeSplit { Id = Guid.CreateVersion7(), IncomeId = income.Id, CategoryId = categoryId, Amount = splitAmount });

            if (categoryMonths.TryGetValue(categoryId, out var categoryMonth))
            {
                categoryMonth.AllocatedAmount = new Money(categoryMonth.AllocatedAmount.Amount + splitAmount.Amount);
            }
        }
    }

    // Takes this income's old splits back out of each category's AllocatedAmount,
    // used before re-splitting an edit and before deleting.
    private async Task UndoSplitsAsync(Income income, CancellationToken cancellationToken)
    {
        var categoryMonths = await _dbContext.CategoryMonths
            .Where(cm => cm.BudgetMonthId == income.BudgetMonthId)
            .ToDictionaryAsync(cm => cm.CategoryId, cancellationToken);

        foreach (var split in income.Splits)
        {
            if (categoryMonths.TryGetValue(split.CategoryId, out var categoryMonth))
            {
                categoryMonth.AllocatedAmount = new Money(Math.Max(0m, categoryMonth.AllocatedAmount.Amount - split.Amount.Amount));
            }
        }

        _dbContext.IncomeSplits.RemoveRange(income.Splits);
    }

    private async Task<Income> LoadOwnedAsync(Guid userId, Guid incomeId, CancellationToken cancellationToken)
    {
        return await _dbContext.Incomes.Include(i => i.Splits)
            .FirstOrDefaultAsync(i => i.Id == incomeId && i.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that income.");
    }

    // A closed month is frozen for good, editing or deleting something that
    // happened in it would silently invalidate numbers that were already rolled
    // over or carried forward.
    private async Task EnsureMonthNotClosedAsync(Guid budgetMonthId, CancellationToken cancellationToken)
    {
        var status = await _dbContext.BudgetMonths.Where(m => m.Id == budgetMonthId).Select(m => m.Status).FirstAsync(cancellationToken);
        if (status == BudgetMonthStatus.Closed)
        {
            throw new ConflictException("This month is closed, nothing can be changed in it any more.");
        }
    }

    // Free accounts can only see a limited window of their own history (docs/00-plan.md
    // section 5), a Pro account or one still on trial gets the real, unclamped date.
    private async Task<DateOnly?> ClampToHistoryWindowAsync(Guid userId, DateOnly? from, CancellationToken cancellationToken)
    {
        var entitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
        if (entitlements.HistoryWindowDays is null)
        {
            return from;
        }

        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var earliestAllowed = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow).AddDays(-entitlements.HistoryWindowDays.Value);
        return from is null || from < earliestAllowed ? earliestAllowed : from;
    }

    private async Task<Dictionary<Guid, Category>> CategoryLookupAsync(IEnumerable<Guid> categoryIds, CancellationToken cancellationToken)
    {
        var ids = categoryIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var categories = await _dbContext.Categories.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);
        return categories.ToDictionary(c => c.Id);
    }

    private static IncomeDto ToDto(Income income, Dictionary<Guid, Category> categories)
    {
        return new IncomeDto(
            income.Id,
            income.Type.ToString(),
            income.TotalAmount.Amount,
            income.Description,
            income.OccurredOn,
            income.Status.ToString(),
            income.Splits.Select(s =>
            {
                categories.TryGetValue(s.CategoryId, out var category);
                return new IncomeSplitDto(s.CategoryId, category?.Name ?? "(deleted category)", s.Amount.Amount);
            }).ToList());
    }
}
