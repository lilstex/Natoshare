using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// The engine every balance in Natoshare is built on. Every write here is one more
// append-only row, nothing is ever changed or removed once it is posted.
public class LedgerService : ILedgerService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;

    public LedgerService(NatoshareDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task PostAsync(
        Guid userId,
        Guid? budgetMonthId,
        AccountRef account,
        LedgerEntryType entryType,
        Money amount,
        LedgerDirection direction,
        SourceTxnType sourceTxnType,
        Guid sourceTxnId,
        string? note,
        CancellationToken cancellationToken = default)
    {
        _dbContext.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BudgetMonthId = budgetMonthId,
            Account = account.Kind,
            AccountCategoryId = account.CategoryId,
            EntryType = entryType,
            Amount = amount,
            Direction = direction,
            SourceTxnType = sourceTxnType,
            SourceTxnId = sourceTxnId,
            Note = note,
            CreatedAt = _clock.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Undoes everything one transaction posted, by writing a matching row in the
    // opposite direction for each one. The original rows stay exactly as they were,
    // this is the only way anything in the ledger is ever "undone".
    public async Task ReverseAsync(SourceTxnType sourceTxnType, Guid sourceTxnId, string? note, CancellationToken cancellationToken = default)
    {
        var originalEntries = await _dbContext.LedgerEntries
            .Where(e => e.SourceTxnType == sourceTxnType && e.SourceTxnId == sourceTxnId && e.EntryType != LedgerEntryType.Reversal)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var entry in originalEntries)
        {
            _dbContext.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.CreateVersion7(),
                UserId = entry.UserId,
                BudgetMonthId = entry.BudgetMonthId,
                Account = entry.Account,
                AccountCategoryId = entry.AccountCategoryId,
                EntryType = LedgerEntryType.Reversal,
                Amount = entry.Amount,
                Direction = entry.Direction == LedgerDirection.Credit ? LedgerDirection.Debit : LedgerDirection.Credit,
                SourceTxnType = sourceTxnType,
                SourceTxnId = sourceTxnId,
                ReversesLedgerEntryId = entry.Id,
                Note = note,
                CreatedAt = now,
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Adds up every Credit and takes away every Debit ever posted to one account,
    // floored at zero. This is "recompute from scratch", the same number a cached
    // balance is always supposed to match.
    public async Task<Money> GetAccountBalanceAsync(Guid userId, AccountRef account, CancellationToken cancellationToken = default)
    {
        var entries = await _dbContext.LedgerEntries
            .Where(e => e.UserId == userId && e.Account == account.Kind && e.AccountCategoryId == account.CategoryId)
            .Select(e => new { e.Amount, e.Direction })
            .ToListAsync(cancellationToken);

        var balance = entries.Sum(e => e.Direction == LedgerDirection.Credit ? e.Amount.Amount : -e.Amount.Amount);
        return new Money(Math.Max(0m, balance));
    }

    public async Task<IReadOnlyDictionary<AccountRef, Money>> GetAccountBalancesAsync(
        Guid userId, IReadOnlyList<AccountRef> accounts, CancellationToken cancellationToken = default)
    {
        if (accounts.Count == 0)
        {
            return new Dictionary<AccountRef, Money>();
        }

        var kinds = accounts.Select(a => a.Kind).Distinct().ToList();
        var categoryIds = accounts.Where(a => a.CategoryId.HasValue).Select(a => a.CategoryId!.Value).Distinct().ToList();

        // One query for every account asked for, instead of one query per account.
        // AccountCategoryId is null only for FlexiblePool, so a row with a null
        // AccountCategoryId always belongs here if its Kind was asked for.
        var rows = await _dbContext.LedgerEntries
            .Where(e => e.UserId == userId && kinds.Contains(e.Account)
                && (e.AccountCategoryId == null || categoryIds.Contains(e.AccountCategoryId.Value)))
            .Select(e => new { e.Account, e.AccountCategoryId, e.Amount, e.Direction })
            .ToListAsync(cancellationToken);

        var sums = rows
            .GroupBy(r => AccountRef.Of(r.Account, r.AccountCategoryId))
            .ToDictionary(
                g => g.Key,
                g => new Money(Math.Max(0m, g.Sum(e => e.Direction == LedgerDirection.Credit ? e.Amount.Amount : -e.Amount.Amount))));

        return accounts.Distinct().ToDictionary(a => a, a => sums.GetValueOrDefault(a, Money.Zero));
    }
}
