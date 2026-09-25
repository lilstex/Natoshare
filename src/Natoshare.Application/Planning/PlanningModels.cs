namespace Natoshare.Application.Planning;

public record RecurringItemDto(
    Guid Id,
    string Kind,
    decimal Amount,
    string Description,
    Guid? CategoryId,
    string? IncomeType,
    string Cadence,
    int AnchorDay,
    string Mode,
    DateOnly NextRunOn,
    DateOnly? LastPostedOn,
    bool IsActive,
    DateTimeOffset CreatedAt);

public record CreateRecurringItemRequest(
    string Kind,
    decimal Amount,
    string Description,
    Guid? CategoryId,
    string? IncomeType,
    string Cadence,
    int AnchorDay,
    string Mode);

public record UpdateRecurringItemRequest(
    decimal? Amount,
    string? Description,
    Guid? CategoryId,
    string? IncomeType,
    string? Cadence,
    int? AnchorDay,
    string? Mode,
    bool? IsActive);

public record CommittedTotalItemDto(Guid Id, string Description, decimal MonthlyAmount);

public record CommittedTotalDto(decimal MonthlyExpenseTotal, IReadOnlyList<CommittedTotalItemDto> Items);
