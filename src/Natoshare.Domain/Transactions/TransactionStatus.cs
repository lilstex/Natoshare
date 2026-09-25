namespace Natoshare.Domain.Transactions;

// Shared by Income and Expense. Editing or deleting one never removes it, it just
// gets marked Reversed and the ledger gets opposite-direction entries, so history is
// never actually lost.
public enum TransactionStatus
{
    Active,
    Reversed,
}
