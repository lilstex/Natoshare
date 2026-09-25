using Natoshare.Domain.Common;
using Natoshare.Domain.Transactions;

namespace Natoshare.Domain.Planning;

public enum RecurringItemKind
{
    Expense,
    Income,
}

public enum RecurringCadence
{
    Monthly,
    Weekly,
    BiWeekly,
}

public enum RecurringItemMode
{
    // Just reminds the user and leaves a draft for them to confirm, nothing is
    // posted on their behalf.
    Remind,

    // Actually creates the income or expense automatically, no confirmation needed.
    AutoPost,
}

// Something that happens on a schedule, like rent or a subscription. This never
// posts anything by itself, a background job (MaterialiseRecurringItems) is what
// reads these and acts on the ones that are due.
public class RecurringItem
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public RecurringItemKind Kind { get; init; }

    public Money Amount { get; set; }

    public string Description { get; set; } = "";

    // Only means something for an Expense, null means it comes out of the Flexible
    // Pool instead of a category.
    public Guid? CategoryId { get; set; }

    // Only means something for an Income.
    public IncomeType? IncomeType { get; set; }

    public RecurringCadence Cadence { get; set; }

    // For Monthly this is a day of month, 1 to 28, or EndOfMonthAnchorDay for "the
    // last day of the month". For Weekly and BiWeekly this is a day of week, using
    // the same numbering as System.DayOfWeek (Sunday = 0).
    public int AnchorDay { get; set; }

    public RecurringItemMode Mode { get; set; }

    public DateOnly NextRunOn { get; set; }

    // Only ever set for AutoPost, Remind never actually posts anything so it has
    // nothing to record here.
    public DateOnly? LastPostedOn { get; set; }

    // A user can pause one without deleting it (or a lapsed trial pauses it for
    // them, see ExpireTrialsAndSubscriptions), the schedule and history stay intact
    // either way.
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; init; }

    // A Monthly item with this AnchorDay means "the last day of the month",
    // whatever that day number actually is that month.
    public const int EndOfMonthAnchorDay = 0;
}
