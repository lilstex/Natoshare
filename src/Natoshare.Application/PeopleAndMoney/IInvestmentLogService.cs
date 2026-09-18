namespace Natoshare.Application.PeopleAndMoney;

public interface IInvestmentLogService
{
    Task<IReadOnlyList<InvestmentLogDto>> ListAsync(
        Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);

    Task<InvestmentLogDto> CreateAsync(Guid userId, CreateInvestmentLogRequest request, CancellationToken cancellationToken = default);

    Task<InvestmentSummaryDto> GetSummaryAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid investmentId, CancellationToken cancellationToken = default);
}
