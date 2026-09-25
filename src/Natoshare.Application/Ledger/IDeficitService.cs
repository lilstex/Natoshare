namespace Natoshare.Application.Ledger;

public interface IDeficitService
{
    Task<IReadOnlyList<DeficitListItemDto>> ListAsync(
        Guid userId, int? year, int? month, string? status, CancellationToken cancellationToken = default);

    // Phase 3 only wires up the three methods that move real money in right now
    // (OwnSavings, OtherCategorySavings, FlexiblePool). NextMonthAllocation is a
    // month-close decision, that lands in Phase 5.
    Task<DeficitResolutionDto> ResolveAsync(Guid userId, ResolveDeficitRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeficitHistoryItemDto>> GetHistoryAsync(
        Guid userId, Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
