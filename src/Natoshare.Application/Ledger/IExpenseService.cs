namespace Natoshare.Application.Ledger;

public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> ListAsync(
        Guid userId,
        string? source,
        Guid? categoryId,
        string? tag,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<ExpenseDto> GetAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken = default);

    Task<LogExpenseResult> CreateAsync(Guid userId, LogExpenseRequest request, CancellationToken cancellationToken = default);

    Task<LogExpenseResult> UpdateAsync(Guid userId, Guid expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid userId, Guid expenseId, CancellationToken cancellationToken = default);

    Task<decimal> GetMonthlyTotalAsync(Guid userId, Guid? categoryId, int year, int month, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagDto>> GetTagsAsync(Guid userId, CancellationToken cancellationToken = default);
}
