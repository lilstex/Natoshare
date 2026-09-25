using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Transactions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// A deliberate move of money from one account to another. Unlike an expense, this can
// never leave a source account short, that is what tells the two apart.
public class ReallocationService : IReallocationService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;

    public ReallocationService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<IReadOnlyList<ReallocationDto>> ListAsync(
        Guid userId, DateOnly? from, DateOnly? to, string? reason, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Reallocations.Where(r => r.UserId == userId);

        if (from is not null)
        {
            query = query.Where(r => r.OccurredOn >= from);
        }

        if (to is not null)
        {
            query = query.Where(r => r.OccurredOn <= to);
        }

        if (reason is not null)
        {
            query = query.Where(r => r.Reason.ToString() == reason);
        }

        var reallocations = await query.OrderByDescending(r => r.OccurredOn).ThenByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
        return reallocations.Select(ToDto).ToList();
    }

    public async Task<ReallocationDto> CreateAsync(Guid userId, CreateReallocationRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var occurredOn = request.OccurredOn ?? UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, occurredOn.Year, occurredOn.Month, cancellationToken);

        var fromAccount = ToAccountRef(request.FromAccount);
        var toAccount = ToAccountRef(request.ToAccount);
        var amount = new Money(request.Amount);
        var reason = Enum.Parse<ReallocationReason>(request.Reason);

        // The one rule that tells a reallocation apart from an expense: the source
        // has to actually have the money, this can never create a deficit.
        var available = await _ledgerService.GetAccountBalanceAsync(userId, fromAccount, cancellationToken);
        if (available < amount)
        {
            throw new ConflictException("The source account does not have enough to cover this.");
        }

        var reallocation = new Reallocation
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BudgetMonthId = budgetMonthId,
            FromAccountKind = fromAccount.Kind,
            FromAccountCategoryId = fromAccount.CategoryId,
            ToAccountKind = toAccount.Kind,
            ToAccountCategoryId = toAccount.CategoryId,
            Amount = amount,
            Reason = reason,
            OccurredOn = occurredOn,
            Note = request.Note,
            CreatedAt = _clock.UtcNow,
        };

        _dbContext.Reallocations.Add(reallocation);

        var isDeficitCover = reason == ReallocationReason.DeficitCover;
        await ApplyToCategoryMonthCacheAsync(budgetMonthId, fromAccount, amount, isCredit: false, isDeficitCover, cancellationToken);
        await ApplyToCategoryMonthCacheAsync(budgetMonthId, toAccount, amount, isCredit: true, isDeficitCover, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var entryType = isDeficitCover ? LedgerEntryType.DeficitCoverage : LedgerEntryType.Transfer;
        await _ledgerService.PostAsync(userId, budgetMonthId, fromAccount, entryType, amount, LedgerDirection.Debit, SourceTxnType.Reallocation, reallocation.Id, request.Note, cancellationToken);
        await _ledgerService.PostAsync(userId, budgetMonthId, toAccount, entryType, amount, LedgerDirection.Credit, SourceTxnType.Reallocation, reallocation.Id, request.Note, cancellationToken);

        return ToDto(reallocation);
    }

    // CategorySavings, FlexiblePool and External balances are always worked out live
    // from the ledger, there is nothing cached for them to update. A Category account
    // is different: its Funded amount is cached on CategoryMonth so balances stay
    // fast, so that is the only side of a reallocation that needs a cache update
    // here. A deficit-cover credit is tracked separately as CoveredAmount (matching
    // the domain model), any other credit into a category counts as more of it being
    // allocated, exactly like income would.
    private async Task ApplyToCategoryMonthCacheAsync(
        Guid budgetMonthId, AccountRef account, Money amount, bool isCredit, bool isDeficitCover, CancellationToken cancellationToken)
    {
        if (account.Kind != AccountKind.Category || account.CategoryId is null)
        {
            return;
        }

        var categoryMonth = await _dbContext.CategoryMonths
            .FirstOrDefaultAsync(cm => cm.BudgetMonthId == budgetMonthId && cm.CategoryId == account.CategoryId, cancellationToken);

        if (categoryMonth is null)
        {
            return;
        }

        if (isCredit && isDeficitCover)
        {
            categoryMonth.CoveredAmount = new Money(categoryMonth.CoveredAmount.Amount + amount.Amount);
        }
        else if (isCredit)
        {
            categoryMonth.AllocatedAmount = new Money(categoryMonth.AllocatedAmount.Amount + amount.Amount);
        }
        else
        {
            categoryMonth.AllocatedAmount = new Money(Math.Max(0m, categoryMonth.AllocatedAmount.Amount - amount.Amount));
        }
    }

    private static AccountRef ToAccountRef(AccountRefInput input) => input.Kind switch
    {
        "Category" => AccountRef.Category(input.CategoryId!.Value),
        "CategorySavings" => AccountRef.CategorySavings(input.CategoryId!.Value),
        "FlexiblePool" => AccountRef.FlexiblePool(),
        _ => throw new ValidationFailedException(new Dictionary<string, string[]> { ["kind"] = [$"'{input.Kind}' is not a valid account kind."] }),
    };

    private static ReallocationDto ToDto(Reallocation r) => new(
        r.Id,
        new AccountRefInput(r.FromAccountKind.ToString(), r.FromAccountCategoryId),
        new AccountRefInput(r.ToAccountKind.ToString(), r.ToAccountCategoryId),
        r.Amount.Amount,
        r.Reason.ToString(),
        r.OccurredOn,
        r.Note);
}
