using FluentAssertions;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;

namespace Natoshare.Application.Tests.Ledger;

public class PacingCalculatorTests
{
    [Fact]
    public void A_category_that_has_not_spent_anything_is_on_track()
    {
        var categoryMonth = new CategoryMonth { AllocatedAmount = new Money(100_000m) };
        var today = new DateOnly(2026, 9, 15);

        var result = PacingCalculator.Compute(categoryMonth, 2026, 9, today);

        result.Status.Should().Be("OnTrack");
        result.Projected.Should().Be(0m);
    }

    [Fact]
    public void Spending_faster_than_planned_projects_over_the_funded_amount()
    {
        // Day 10 of a 30 day month, already spent half the month's budget.
        var categoryMonth = new CategoryMonth { AllocatedAmount = new Money(30_000m), SpentAmount = new Money(15_000m) };
        var today = new DateOnly(2026, 9, 10);

        var result = PacingCalculator.Compute(categoryMonth, 2026, 9, today);

        // Projected = 15000 / 10 * 30 = 45000, well past the 30000 funded.
        result.Projected.Should().Be(45_000m);
        result.Status.Should().Be("OverPace");
    }

    [Fact]
    public void A_category_already_in_deficit_is_always_reported_as_in_deficit_even_if_pace_looks_fine()
    {
        var categoryMonth = new CategoryMonth { AllocatedAmount = new Money(10_000m), SpentAmount = new Money(12_000m) };
        var today = new DateOnly(2026, 9, 29); // near month end, "pace" alone would look calm

        var result = PacingCalculator.Compute(categoryMonth, 2026, 9, today);

        result.Status.Should().Be("InDeficit");
    }

    [Fact]
    public void Safe_to_spend_is_the_remaining_funded_amount_spread_across_the_days_left()
    {
        var categoryMonth = new CategoryMonth { AllocatedAmount = new Money(30_000m), SpentAmount = new Money(9_000m) };
        var today = new DateOnly(2026, 9, 10); // 21 days left including today, in a 30 day month

        var result = PacingCalculator.Compute(categoryMonth, 2026, 9, today);

        // (30000 - 9000) / 21 = 1000
        result.SafeToSpendDaily.Should().Be(1_000m);
    }
}
