using Natoshare.Domain.Common;

namespace Natoshare.Domain.Budgeting;

public enum BudgetMonthStatus
{
    Open,
    Closed,
}

// One user's month. It comes into existence the moment they log their first income or
// expense in it, not before, and it freezes which allocation version and which fixed
// income amount governs that month forever, even if the user changes their setup
// later, so past months never silently rewrite themselves.
public class BudgetMonth
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public int Year { get; init; }

    public int Month { get; init; }

    public BudgetMonthStatus Status { get; set; } = BudgetMonthStatus.Open;

    public Guid AllocationConfigVersionId { get; init; }

    public Money FixedIncomeSnapshot { get; init; }

    public DateTimeOffset OpenedAt { get; init; }

    public DateTimeOffset? ClosedAt { get; set; }

    public Guid? ClosedByUserId { get; set; }

    public List<CategoryMonth> CategoryMonths { get; set; } = [];
}
