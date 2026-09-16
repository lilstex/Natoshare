namespace Natoshare.Application.Ledger;

// Resolves and opens a user's months. A month only ever comes into existence the
// moment something real happens in it, viewing one never creates it.
public interface IBudgetMonthService
{
    // Read-only: what this month looks like right now. If nothing has happened in it
    // yet, this works out what it WOULD look like without saving anything, so a
    // balances screen can show real numbers even before the user's first income.
    Task<MonthSnapshot> GetSnapshotAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    // Makes sure this month actually exists in the database (creating the BudgetMonth
    // and CategoryMonth rows and posting the Allocation ledger entries, the first
    // time), and returns its id. Safe to call more than once, a month only opens
    // once.
    Task<Guid> EnsureOpenAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);
}
