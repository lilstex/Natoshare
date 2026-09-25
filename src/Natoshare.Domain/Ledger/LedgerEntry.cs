using Natoshare.Domain.Common;

namespace Natoshare.Domain.Ledger;

// One line in the ledger. Once written it is never changed and never removed, every
// balance anywhere in Natoshare is just adding up these rows. To undo one, we write a
// new row in the opposite direction, we do not touch the old one.
public class LedgerEntry
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid? BudgetMonthId { get; init; }

    public AccountKind Account { get; init; }

    public Guid? AccountCategoryId { get; init; }

    public LedgerEntryType EntryType { get; init; }

    public Money Amount { get; init; }

    public LedgerDirection Direction { get; init; }

    public SourceTxnType SourceTxnType { get; init; }

    public Guid SourceTxnId { get; init; }

    public Guid? ReversesLedgerEntryId { get; init; }

    public string? Note { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
