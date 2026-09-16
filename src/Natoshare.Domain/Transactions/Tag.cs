namespace Natoshare.Domain.Transactions;

// A user's own label for grouping expenses, like "work" or "birthday". Shared across
// every expense that uses it instead of being copied onto each one.
public class Tag
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string Name { get; init; } = "";
}

// Joins one Expense to one Tag. Kept as its own row (instead of a plain list of tag
// ids on Expense) so a tag's usage count is a simple count of this table.
public class ExpenseTag
{
    public Guid ExpenseId { get; init; }

    public Guid TagId { get; init; }
}
