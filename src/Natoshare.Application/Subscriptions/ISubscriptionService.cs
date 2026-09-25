namespace Natoshare.Application.Subscriptions;

public interface ISubscriptionService
{
    Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default);

    // Payments are stubbed (docs/00-plan.md section 5): this always creates a
    // Pending record for an admin to activate by hand, it never charges anyone.
    Task<UpgradeResultDto> UpgradeAsync(Guid userId, UpgradeSubscriptionRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionRecordDto>> GetHistoryAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ActivateAsync(Guid adminUserId, string reference, ActivateSubscriptionRequest request, CancellationToken cancellationToken = default);

    // For the admin app's subscriptions queue. Null status means every record, not
    // just one user's, which is why this takes no userId unlike GetHistoryAsync.
    Task<IReadOnlyList<AdminSubscriptionRecordDto>> ListAsync(string? status, CancellationToken cancellationToken = default);
}
