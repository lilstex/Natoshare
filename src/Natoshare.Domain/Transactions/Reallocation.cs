using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;

namespace Natoshare.Domain.Transactions;

public enum ReallocationReason
{
    DeficitCover,
    MonthCloseRebalance,
    FundPromise,
    FundLoan,
    Manual,
}

// A deliberate move of money from one account to another. Unlike an expense, this can
// never create a deficit, the source has to actually have the money already, that is
// what tells the two apart.
public class Reallocation
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid BudgetMonthId { get; init; }

    public AccountKind FromAccountKind { get; init; }

    public Guid? FromAccountCategoryId { get; init; }

    public AccountKind ToAccountKind { get; init; }

    public Guid? ToAccountCategoryId { get; init; }

    public Money Amount { get; init; }

    public ReallocationReason Reason { get; init; }

    public DateOnly OccurredOn { get; init; }

    public string? Note { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
