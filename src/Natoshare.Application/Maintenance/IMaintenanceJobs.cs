namespace Natoshare.Application.Maintenance;

// The housekeeping jobs that keep accounts, deletions and the ledger itself honest
// over time. See docs/03-architecture.md's Hangfire table for the schedule each one
// runs on.
public interface IMaintenanceJobs
{
    // Pauses (does not delete) every active RecurringItem owned by an account whose
    // trial has ended, this is the one place in the app today where a lapsed trial
    // actually changes something, the real Plan/Subscription downgrade is Phase 9.
    Task ExpireTrialsAndSubscriptionsAsync(CancellationToken cancellationToken = default);

    // Hard-deletes any account that has sat in PendingDeletion past its grace
    // period, and everything it owns.
    Task PurgePendingDeletionsAsync(CancellationToken cancellationToken = default);

    // Re-checks the one invariant month close already depends on: once a category's
    // most recently closed month is Closed and nothing newer has touched it since,
    // its ledger account should net to exactly zero. Logs an error for any drift
    // found (it never tries to fix anything itself) and returns how many categories
    // had drift, so a test can inject a deliberate mismatch and assert on it
    // directly instead of having to intercept a log line.
    Task<int> RunLedgerIntegrityCheckAsync(CancellationToken cancellationToken = default);
}
