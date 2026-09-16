namespace Natoshare.Domain.Budgeting;

// How a category's deficit got cleared. The first three move real money in right now,
// Natoshare only actually wires those up in Phase 3, NextMonthAllocation defers the
// fix to when the month closes (Phase 5). Mixed and None only ever show up on a
// CategoryMonth once month close exists, never as a choice someone picks.
public enum DeficitResolutionMethod
{
    OwnSavings,
    OtherCategorySavings,
    FlexiblePool,
    NextMonthAllocation,
    Mixed,
    None,
}
