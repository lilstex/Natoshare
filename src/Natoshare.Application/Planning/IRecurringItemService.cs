namespace Natoshare.Application.Planning;

// Everything a user can do to their own recurring items. Every method here needs an
// active Pro entitlement (trial or paid), 💳 in docs/02-api-surface.md, see
// IEntitlementService.
public interface IRecurringItemService
{
    Task<IReadOnlyList<RecurringItemDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<RecurringItemDto> CreateAsync(Guid userId, CreateRecurringItemRequest request, CancellationToken cancellationToken = default);

    Task<RecurringItemDto> UpdateAsync(
        Guid userId, Guid recurringItemId, UpdateRecurringItemRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid recurringItemId, CancellationToken cancellationToken = default);

    // Moves NextRunOn on to the following cycle without posting or reminding for
    // the one being skipped, for "not this month" without deleting the whole thing.
    Task<RecurringItemDto> SkipNextAsync(Guid userId, Guid recurringItemId, CancellationToken cancellationToken = default);

    Task<CommittedTotalDto> GetCommittedTotalAsync(Guid userId, CancellationToken cancellationToken = default);
}
