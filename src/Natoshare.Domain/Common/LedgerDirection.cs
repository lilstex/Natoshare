namespace Natoshare.Domain.Common;

// This tells us which way money is moving on a ledger entry.
// Credit means money is coming into the account. Debit means money is going out.
// We never use a minus sign for this, the direction lives here instead.
public enum LedgerDirection
{
    Credit,
    Debit
}
