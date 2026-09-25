namespace Natoshare.Application.Common;

// Everything a plan actually grants right now, resolved from Free/Pro plus an
// active trial. Matches GET /me/entitlements exactly, see docs/02-api-surface.md.
public record PlanEntitlements(
    string Plan,
    bool IsTrial,
    DateTimeOffset TrialEndsAt,
    int? MaxCategories,
    int? HistoryWindowDays,
    bool SinkingFund,
    bool DeficitCoverFromSavings,
    bool Recurring,
    bool Export);

// Works out what an account is actually allowed to do: Free vs Pro, with a trial
// counting as full Pro access for as long as it runs. Every plan-gated check in the
// app (recurring items, report export, the category limit, sinking-fund carry-over,
// covering a deficit from savings, the transaction history window) goes through
// this one place, so the rules in docs/00-plan.md section 5 only ever live here.
public interface IEntitlementService
{
    Task<PlanEntitlements> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);

    // Throws UpgradeRequiredException naming the feature if the resolved plan does
    // not include it. Used for the simple on/off features (recurring items, export).
    Task EnsureEntitledAsync(Guid userId, string featureName, CancellationToken cancellationToken = default);

    // Which of a user's own categories are currently locked for being over the
    // Free-plan limit, ordered by SortOrder so the same ones stay locked/unlocked
    // consistently instead of shuffling around. Empty on Pro or during a trial,
    // there is no limit to be over. The categories themselves are never touched,
    // only reads (like GET /categories) and writes (like logging an expense
    // against one) treat them differently, "data is kept, not deleted."
    Task<IReadOnlySet<Guid>> GetLockedCategoryIdsAsync(Guid userId, CancellationToken cancellationToken = default);
}
