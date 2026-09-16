using Natoshare.Domain.Common;

namespace Natoshare.Domain.Budgeting;

// One category's numbers for one month. Available and Deficit are never stored on
// their own, they always come from working out these fields, so they can never drift
// out of sync with what actually happened.
public class CategoryMonth
{
    public Guid Id { get; init; }

    public Guid BudgetMonthId { get; init; }

    public Guid CategoryId { get; init; }

    public Money AllocatedAmount { get; set; }

    public Money CarriedInSavings { get; init; } = Money.Zero;

    public Money CarriedInDeficit { get; init; } = Money.Zero;

    public Money SpentAmount { get; set; } = Money.Zero;

    public Money CoveredAmount { get; set; } = Money.Zero;

    public bool ExternalTransferConfirmed { get; set; }

    public Money? ExternalTransferAmount { get; set; }

    public DateTimeOffset? ExternalTransferConfirmedAt { get; set; }

    // Everything below here stays empty until month close exists (Phase 5), it is
    // only ever written once a month is actually closed.
    public Money? SavedThisMonth { get; set; }

    public Money? DeficitAtClose { get; set; }

    public DeficitResolutionMethod? DeficitResolvedVia { get; set; }

    public Money? CarriedOutSavings { get; set; }

    public Money? CarriedOutDeficit { get; set; }

    // How much this category can actually draw on this month. This can dip below
    // zero internally (for example a carried-in deficit bigger than this month's
    // allocation), which is exactly why it is a plain decimal and not a Money, Money
    // can never hold a negative number. Available and Deficit below turn this back
    // into the non-negative numbers the rest of the app is allowed to see.
    private decimal FundedRaw() =>
        AllocatedAmount.Amount + CarriedInSavings.Amount + CoveredAmount.Amount
        - CarriedInDeficit.Amount - (ExternalTransferAmount?.Amount ?? 0m);

    // The same number as FundedRaw, but never negative, for anything that has to show
    // "funded" to a user or an API response.
    public Money Funded() => new(Math.Max(0m, FundedRaw()));

    public Money Available() => new(Math.Max(0m, FundedRaw() - SpentAmount.Amount));

    public Money Deficit() => new(Math.Max(0m, SpentAmount.Amount - FundedRaw()));
}
