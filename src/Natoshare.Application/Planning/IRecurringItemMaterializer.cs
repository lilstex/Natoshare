namespace Natoshare.Application.Planning;

// Runs hourly (see docs/03-architecture.md's Hangfire table): finds every active
// RecurringItem whose NextRunOn has arrived, either posts the real transaction
// (AutoPost) or raises a reminder (Remind), and moves NextRunOn on to the next
// occurrence either way. Skips anything owned by an account whose entitlement has
// lapsed, the same rule the API itself enforces.
public interface IRecurringItemMaterializer
{
    Task MaterializeDueItemsAsync(CancellationToken cancellationToken = default);
}
