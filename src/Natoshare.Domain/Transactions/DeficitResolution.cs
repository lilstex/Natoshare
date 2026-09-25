using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;

namespace Natoshare.Domain.Transactions;

// The audit record for how one category's deficit got cleared. Kept even after the
// money moves so reports and history can always show how a shortfall was actually
// covered.
public class DeficitResolution
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid BudgetMonthId { get; init; }

    public Guid CategoryId { get; init; }

    public Money Amount { get; init; }

    public DeficitResolutionMethod Method { get; init; }

    public Guid? SourceCategoryId { get; init; }

    public DateOnly ResolvedOn { get; init; }

    public Guid ResolvedByUserId { get; init; }

    public string? Note { get; init; }

    public Guid? ReallocationId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
