namespace Natoshare.Application.Ledger;

public interface IIncomeService
{
    Task<IReadOnlyList<IncomeDto>> ListAsync(
        Guid userId, string? type, DateOnly? from, DateOnly? to, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<IncomeDto> GetAsync(Guid userId, Guid incomeId, CancellationToken cancellationToken = default);

    Task<IncomeDto> CreateAsync(Guid userId, LogIncomeRequest request, CancellationToken cancellationToken = default);

    Task<IncomeDto> UpdateAsync(Guid userId, Guid incomeId, UpdateIncomeRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid incomeId, CancellationToken cancellationToken = default);
}
