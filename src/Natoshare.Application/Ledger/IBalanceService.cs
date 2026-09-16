namespace Natoshare.Application.Ledger;

public interface IBalanceService
{
    Task<BalancesResult> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BalanceHistoryItemDto>> GetHistoryAsync(
        Guid userId, Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
}
