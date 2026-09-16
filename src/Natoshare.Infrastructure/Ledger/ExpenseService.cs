using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Transactions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// Money going out. This never blocks, even when it pushes a category into deficit,
// the app just shows that honestly and tells the caller so, instead of stopping a
// real expense from being recorded.
public class ExpenseService : IExpenseService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;

    public ExpenseService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<IReadOnlyList<ExpenseDto>> ListAsync(
        Guid userId,
        string? source,
        Guid? categoryId,
        string? tag,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Expenses.Include(e => e.ExpenseTags).Where(e => e.UserId == userId);

        if (source is not null)
        {
            query = query.Where(e => e.Source.ToString() == source);
        }

        if (categoryId is not null)
        {
            query = query.Where(e => e.CategoryId == categoryId);
        }

        if (from is not null)
        {
            query = query.Where(e => e.OccurredOn >= from);
        }

        if (to is not null)
        {
            query = query.Where(e => e.OccurredOn <= to);
        }

        if (tag is not null)
        {
            var tagId = await _dbContext.Tags.Where(t => t.UserId == userId && t.Name == tag).Select(t => (Guid?)t.Id).FirstOrDefaultAsync(cancellationToken);
            query = query.Where(e => e.ExpenseTags.Any(et => et.TagId == tagId));
        }

        var expenses = await query
            .OrderByDescending(e => e.OccurredOn).ThenByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        var tagNames = await TagNameLookupAsync(userId, cancellationToken);
        return expenses.Select(e => ToDto(e, tagNames)).ToList();
    }

    public async Task<ExpenseDto> GetAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await LoadOwnedAsync(userId, expenseId, cancellationToken);
        var tagNames = await TagNameLookupAsync(userId, cancellationToken);
        return ToDto(expense, tagNames);
    }

    public async Task<LogExpenseResult> CreateAsync(Guid userId, LogExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var occurredOn = request.OccurredOn ?? UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, occurredOn.Year, occurredOn.Month, cancellationToken);

        var source = request.Source == "Category" ? ExpenseSource.Category : ExpenseSource.FlexiblePool;
        var amount = new Money(request.Amount);

        CategoryMonth? categoryMonth = null;
        if (source == ExpenseSource.Category)
        {
            categoryMonth = await _dbContext.CategoryMonths
                .FirstOrDefaultAsync(cm => cm.BudgetMonthId == budgetMonthId && cm.CategoryId == request.CategoryId!.Value, cancellationToken)
                ?? throw new NotFoundException("This category is not part of this month's split.");
        }

        var expense = new Expense
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Amount = amount,
            Description = request.Description,
            Source = source,
            CategoryId = request.CategoryId,
            SubCategory = request.SubCategory,
            OccurredOn = occurredOn,
            BudgetMonthId = budgetMonthId,
            CreatedAt = _clock.UtcNow,
        };

        var tags = await EnsureTagsExistAsync(userId, request.Tags, cancellationToken);
        expense.ExpenseTags = tags.Select(t => new ExpenseTag { ExpenseId = expense.Id, TagId = t.Id }).ToList();

        _dbContext.Expenses.Add(expense);

        if (categoryMonth is not null)
        {
            categoryMonth.SpentAmount = new Money(categoryMonth.SpentAmount.Amount + amount.Amount);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var account = source == ExpenseSource.Category ? AccountRef.Category(request.CategoryId!.Value) : AccountRef.FlexiblePool();
        await _ledgerService.PostAsync(
            userId, budgetMonthId, account, LedgerEntryType.Expense, amount, LedgerDirection.Debit, SourceTxnType.Expense, expense.Id, request.Description, cancellationToken);

        return await BuildResultAsync(expense, categoryMonth, tags, user.TimeZoneId, cancellationToken);
    }

    public async Task<LogExpenseResult> UpdateAsync(Guid userId, Guid expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken = default)
    {
        var expense = await LoadOwnedAsync(userId, expenseId, cancellationToken);
        if (expense.Status == TransactionStatus.Reversed)
        {
            throw new ConflictException("This expense has already been removed.");
        }

        var newOccurredOn = request.OccurredOn ?? expense.OccurredOn;
        if (newOccurredOn.Year != expense.OccurredOn.Year || newOccurredOn.Month != expense.OccurredOn.Month)
        {
            throw new ConflictException("Moving an expense to a different month is not supported yet, delete it and log a fresh one instead.");
        }

        if (request.CategoryId is not null && expense.Source != ExpenseSource.Category)
        {
            throw new ConflictException("A Flexible Pool expense cannot be moved to a category, delete it and log a fresh one instead.");
        }

        var newAmount = request.Amount.HasValue ? new Money(request.Amount.Value) : expense.Amount;
        var newCategoryId = request.CategoryId ?? expense.CategoryId;

        await _ledgerService.ReverseAsync(SourceTxnType.Expense, expense.Id, "Edited", cancellationToken);

        // Take the old amount back out of wherever it was spent from.
        if (expense.Source == ExpenseSource.Category && expense.CategoryId is not null)
        {
            var oldCategoryMonth = await _dbContext.CategoryMonths
                .FirstOrDefaultAsync(cm => cm.BudgetMonthId == expense.BudgetMonthId && cm.CategoryId == expense.CategoryId, cancellationToken);
            if (oldCategoryMonth is not null)
            {
                oldCategoryMonth.SpentAmount = new Money(Math.Max(0m, oldCategoryMonth.SpentAmount.Amount - expense.Amount.Amount));
            }
        }

        CategoryMonth? newCategoryMonth = null;
        if (expense.Source == ExpenseSource.Category)
        {
            newCategoryMonth = await _dbContext.CategoryMonths
                .FirstOrDefaultAsync(cm => cm.BudgetMonthId == expense.BudgetMonthId && cm.CategoryId == newCategoryId, cancellationToken)
                ?? throw new NotFoundException("This category is not part of this month's split.");
            newCategoryMonth.SpentAmount = new Money(newCategoryMonth.SpentAmount.Amount + newAmount.Amount);
        }

        expense.Amount = newAmount;
        expense.Description = request.Description ?? expense.Description;
        expense.CategoryId = newCategoryId;
        expense.SubCategory = request.SubCategory ?? expense.SubCategory;
        expense.OccurredOn = newOccurredOn;

        if (request.Tags is not null)
        {
            _dbContext.ExpenseTags.RemoveRange(expense.ExpenseTags);
            var tags = await EnsureTagsExistAsync(userId, request.Tags, cancellationToken);
            expense.ExpenseTags = tags.Select(t => new ExpenseTag { ExpenseId = expense.Id, TagId = t.Id }).ToList();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var account = expense.Source == ExpenseSource.Category ? AccountRef.Category(expense.CategoryId!.Value) : AccountRef.FlexiblePool();
        await _ledgerService.PostAsync(
            userId, expense.BudgetMonthId, account, LedgerEntryType.Expense, newAmount, LedgerDirection.Debit, SourceTxnType.Expense, expense.Id, expense.Description, cancellationToken);

        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var resultTags = await TagsForExpenseAsync(expense, cancellationToken);
        return await BuildResultAsync(expense, newCategoryMonth, resultTags, user.TimeZoneId, cancellationToken);
    }

    public async Task DeleteAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken = default)
    {
        var expense = await LoadOwnedAsync(userId, expenseId, cancellationToken);
        if (expense.Status == TransactionStatus.Reversed)
        {
            return;
        }

        await _ledgerService.ReverseAsync(SourceTxnType.Expense, expense.Id, "Deleted", cancellationToken);

        if (expense.Source == ExpenseSource.Category && expense.CategoryId is not null)
        {
            var categoryMonth = await _dbContext.CategoryMonths
                .FirstOrDefaultAsync(cm => cm.BudgetMonthId == expense.BudgetMonthId && cm.CategoryId == expense.CategoryId, cancellationToken);
            if (categoryMonth is not null)
            {
                categoryMonth.SpentAmount = new Money(Math.Max(0m, categoryMonth.SpentAmount.Amount - expense.Amount.Amount));
            }
        }

        expense.Status = TransactionStatus.Reversed;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> GetMonthlyTotalAsync(Guid userId, Guid? categoryId, int year, int month, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Expenses.Where(e =>
            e.UserId == userId && e.Status == TransactionStatus.Active && e.OccurredOn.Year == year && e.OccurredOn.Month == month);

        if (categoryId is not null)
        {
            query = query.Where(e => e.CategoryId == categoryId);
        }

        return await query.SumAsync(e => e.Amount.Amount, cancellationToken);
    }

    public async Task<IReadOnlyList<TagDto>> GetTagsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tags = await _dbContext.Tags
            .Where(t => t.UserId == userId)
            .Select(t => new TagDto(t.Id, t.Name, _dbContext.ExpenseTags.Count(et => et.TagId == t.Id)))
            .ToListAsync(cancellationToken);

        return tags;
    }

    private async Task<LogExpenseResult> BuildResultAsync(
        Expense expense, CategoryMonth? categoryMonth, List<Tag> tags, string timeZoneId, CancellationToken cancellationToken)
    {
        var dto = ToDto(expense, tags.ToDictionary(t => t.Id, t => t.Name));

        if (categoryMonth is null)
        {
            return new LogExpenseResult(dto, new PacingDto("NotApplicable", 0m, 0m), false, null);
        }

        var today = UserTime.TodayFor(timeZoneId, _clock.UtcNow);
        var pace = PacingCalculator.Compute(categoryMonth, expense.OccurredOn.Year, expense.OccurredOn.Month, today);
        var pacing = new PacingDto(pace.Status, pace.Projected, pace.SafeToSpendDaily);

        var deficit = categoryMonth.Deficit();
        var wentIntoDeficit = deficit > Money.Zero;

        return new LogExpenseResult(
            dto, pacing, wentIntoDeficit, wentIntoDeficit ? new ExpenseDeficitDto(categoryMonth.CategoryId, deficit.Amount) : null);
    }

    private async Task<List<Tag>> EnsureTagsExistAsync(Guid userId, List<string>? tagNames, CancellationToken cancellationToken)
    {
        if (tagNames is null || tagNames.Count == 0)
        {
            return [];
        }

        var trimmedNames = tagNames
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await _dbContext.Tags.Where(t => t.UserId == userId).ToListAsync(cancellationToken);
        var result = new List<Tag>();

        foreach (var name in trimmedNames)
        {
            var match = existing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                match = new Tag { Id = Guid.CreateVersion7(), UserId = userId, Name = name };
                _dbContext.Tags.Add(match);
                existing.Add(match);
            }

            result.Add(match);
        }

        return result;
    }

    private async Task<List<Tag>> TagsForExpenseAsync(Expense expense, CancellationToken cancellationToken)
    {
        var tagIds = expense.ExpenseTags.Select(t => t.TagId).ToList();
        return await _dbContext.Tags.Where(t => tagIds.Contains(t.Id)).ToListAsync(cancellationToken);
    }

    private async Task<Expense> LoadOwnedAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken)
    {
        return await _dbContext.Expenses.Include(e => e.ExpenseTags)
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that expense.");
    }

    private async Task<Dictionary<Guid, string>> TagNameLookupAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.Tags.Where(t => t.UserId == userId).ToDictionaryAsync(t => t.Id, t => t.Name, cancellationToken);
    }

    private static ExpenseDto ToDto(Expense expense, Dictionary<Guid, string> tagNames)
    {
        var tags = expense.ExpenseTags
            .Select(t => tagNames.TryGetValue(t.TagId, out var name) ? name : null)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToList();

        return new ExpenseDto(
            expense.Id, expense.Amount.Amount, expense.Description, expense.Source.ToString(), expense.CategoryId,
            expense.SubCategory, expense.OccurredOn, expense.Status.ToString(), tags);
    }
}
