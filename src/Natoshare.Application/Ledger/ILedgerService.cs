using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;

namespace Natoshare.Application.Ledger;

// The engine every balance in Natoshare is built on. Nothing anywhere is allowed to
// just add or subtract a number from a "balance" column, every change to what an
// account holds goes through here, as an append-only row.
public interface ILedgerService
{
    Task PostAsync(
        Guid userId,
        Guid? budgetMonthId,
        AccountRef account,
        LedgerEntryType entryType,
        Money amount,
        LedgerDirection direction,
        SourceTxnType sourceTxnType,
        Guid sourceTxnId,
        string? note,
        CancellationToken cancellationToken = default);

    // Undoes everything one transaction wrote to the ledger, by posting a matching
    // row in the opposite direction for each one. The original rows are never
    // touched, this is how Natoshare edits and deletes things without ever losing
    // history.
    Task ReverseAsync(SourceTxnType sourceTxnType, Guid sourceTxnId, string? note, CancellationToken cancellationToken = default);

    // Adds up every Credit and takes away every Debit ever posted to one account,
    // floored at zero. This is "recompute from scratch", the same number a cached
    // balance is supposed to always match.
    Task<Money> GetAccountBalanceAsync(Guid userId, AccountRef account, CancellationToken cancellationToken = default);
}
