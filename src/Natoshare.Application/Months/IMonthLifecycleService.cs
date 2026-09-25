namespace Natoshare.Application.Months;

public interface IMonthLifecycleService
{
    Task<IReadOnlyList<MonthSummaryListItemDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<MonthDetailDto> GetDetailAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    Task<MonthDetailDto> OpenEarlyAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    Task<ClosePreviewResult> GetClosePreviewAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    Task<CategoryMonthDetailDto> ConfirmFixedAccountAsync(
        Guid userId, int year, int month, ConfirmFixedAccountRequest request, CancellationToken cancellationToken = default);

    // Closing is blocked (409) while any category still has an unresolved deficit,
    // one deficitResolutions entry is required per category still in deficit.
    Task<CloseMonthResult> CloseAsync(Guid userId, int year, int month, CloseMonthRequest request, CancellationToken cancellationToken = default);

    // Admin only. Undoes what closing posted (rollovers and carried-forward
    // deficits) and puts the month back to Open. Reallocations that already moved
    // real money (a mid-month deficit cover, a fixed account confirmation) are left
    // standing, reopening is about letting someone keep working on the month, not a
    // full rewind of every choice already made in it.
    Task ReopenAsync(Guid adminUserId, Guid targetUserId, int year, int month, string reason, CancellationToken cancellationToken = default);

    // Run daily by a recurring job: reminds a user if a month is still sitting Open
    // well after it ended, so an old month does not just get forgotten about.
    Task EvaluateCloseRemindersAsync(CancellationToken cancellationToken = default);
}
