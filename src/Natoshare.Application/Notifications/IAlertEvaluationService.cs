namespace Natoshare.Application.Notifications;

// Works out whether a category's current numbers deserve an alert, and writes one if
// so (deduplicated so the same alert never fires twice in one day). Called two ways:
// right after a write that could affect one category, and by a recurring job that
// re-checks every user's current month for the alerts only time passing can trigger
// (pacing and safe-to-spend do not need a new write to go stale).
public interface IAlertEvaluationService
{
    Task EvaluateCategoryAsync(Guid userId, Guid budgetMonthId, Guid categoryId, CancellationToken cancellationToken = default);

    Task EvaluateAllOpenMonthsAsync(CancellationToken cancellationToken = default);

    // Run once a day by a recurring job: debts and loans coming due or already
    // overdue, and promises the user can now afford to redeem.
    Task EvaluateObligationsAsync(CancellationToken cancellationToken = default);
}
