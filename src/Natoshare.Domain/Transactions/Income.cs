using Natoshare.Domain.Common;

namespace Natoshare.Domain.Transactions;

public enum IncomeType
{
    Allocatable,
    Flexible,
}

// Money coming in. Allocatable income gets split across the user's categories the
// moment it is logged, Flexible income (a gift, a refund, anything not part of the
// regular budget) just goes straight into the Flexible Pool untouched.
public class Income
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public IncomeType Type { get; init; }

    public Money TotalAmount { get; set; }

    public string Description { get; set; } = "";

    public DateOnly OccurredOn { get; set; }

    public Guid BudgetMonthId { get; init; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    public DateTimeOffset CreatedAt { get; init; }

    public List<IncomeSplit> Splits { get; set; } = [];
}

// One category's share of an Allocatable income. Every split for one Income always
// adds up to exactly that Income's TotalAmount, down to the last kobo or cent.
public class IncomeSplit
{
    public Guid Id { get; init; }

    public Guid IncomeId { get; init; }

    public Guid CategoryId { get; init; }

    public Money Amount { get; init; }
}
