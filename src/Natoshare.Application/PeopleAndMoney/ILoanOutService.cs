namespace Natoshare.Application.PeopleAndMoney;

public interface ILoanOutService
{
    Task<IReadOnlyList<LoanOutDto>> ListAsync(Guid userId, string? status, CancellationToken cancellationToken = default);

    Task<LoanOutDto> CreateAsync(Guid userId, CreateLoanOutRequest request, CancellationToken cancellationToken = default);

    Task<LoanOutDto> AddRepaymentAsync(
        Guid userId, Guid loanId, CreateLoanRepaymentRequest request, CancellationToken cancellationToken = default);

    Task<LoanOutDto> UpdateAsync(Guid userId, Guid loanId, UpdateLoanOutRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid loanId, CancellationToken cancellationToken = default);
}
