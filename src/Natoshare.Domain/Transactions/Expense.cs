using Natoshare.Domain.Common;

namespace Natoshare.Domain.Transactions;

public enum ExpenseSource
{
    Category,
    FlexiblePool,
}

// Money going out. This is never blocked, even when it pushes a category over what
// it was funded, the category just moves into deficit and the app shows that
// honestly instead of stopping the user from recording something real.
public class Expense
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Money Amount { get; set; }

    public string Description { get; set; } = "";

    public ExpenseSource Source { get; init; }

    public Guid? CategoryId { get; set; }

    public string? SubCategory { get; set; }

    public DateOnly OccurredOn { get; set; }

    public Guid BudgetMonthId { get; init; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    public DateTimeOffset CreatedAt { get; init; }

    public List<ExpenseTag> ExpenseTags { get; set; } = [];
}
