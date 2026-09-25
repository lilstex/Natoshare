namespace Natoshare.Application.PeopleAndMoney;

public interface IDebtInService
{
    Task<IReadOnlyList<DebtInDto>> ListAsync(Guid userId, string? status, CancellationToken cancellationToken = default);

    Task<DebtInDto> CreateAsync(Guid userId, CreateDebtInRequest request, CancellationToken cancellationToken = default);

    Task<DebtInDto> AddRepaymentAsync(
        Guid userId, Guid debtId, CreateDebtRepaymentRequest request, CancellationToken cancellationToken = default);

    Task<DebtInDto> UpdateAsync(Guid userId, Guid debtId, UpdateDebtInRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid debtId, CancellationToken cancellationToken = default);
}
