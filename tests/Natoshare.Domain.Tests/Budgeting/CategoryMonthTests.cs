using FluentAssertions;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;

namespace Natoshare.Domain.Tests.Budgeting;

public class CategoryMonthTests
{
    [Fact]
    public void A_category_with_no_spend_is_fully_available()
    {
        var month = new CategoryMonth { AllocatedAmount = new Money(100_000m) };

        month.Available().Should().Be(new Money(100_000m));
        month.Deficit().Should().Be(Money.Zero);
        month.Funded().Should().Be(new Money(100_000m));
    }

    [Fact]
    public void Spending_more_than_funded_shows_as_deficit_not_a_negative_available()
    {
        var month = new CategoryMonth { AllocatedAmount = new Money(75_000m), SpentAmount = new Money(95_000m) };

        month.Available().Should().Be(Money.Zero);
        month.Deficit().Should().Be(new Money(20_000m));
    }

    [Fact]
    public void Covered_amount_from_a_deficit_resolution_increases_what_is_funded()
    {
        var month = new CategoryMonth
        {
            AllocatedAmount = new Money(75_000m),
            SpentAmount = new Money(95_000m),
            CoveredAmount = new Money(20_000m),
        };

        month.Funded().Should().Be(new Money(95_000m));
        month.Deficit().Should().Be(Money.Zero);
        month.Available().Should().Be(Money.Zero);
    }

    [Fact]
    public void A_carried_in_deficit_bigger_than_the_allocation_already_shows_as_a_deficit_before_any_spend()
    {
        // This can only happen once month close exists (Phase 5) and carries a
        // deficit forward bigger than next month's allocation, but the math has to
        // hold up correctly even then, Funded must never be allowed to look negative,
        // the shortfall shows up as a Deficit instead.
        var month = new CategoryMonth
        {
            AllocatedAmount = new Money(10_000m),
            CarriedInDeficit = new Money(15_000m),
        };

        month.Funded().Should().Be(Money.Zero);
        month.Deficit().Should().Be(new Money(5_000m));
        month.Available().Should().Be(Money.Zero);
    }

    [Fact]
    public void An_external_transfer_reduces_what_is_still_funded_in_the_app()
    {
        var month = new CategoryMonth
        {
            AllocatedAmount = new Money(125_000m),
            ExternalTransferAmount = new Money(125_000m),
        };

        month.Funded().Should().Be(Money.Zero);
        month.Available().Should().Be(Money.Zero);
    }
}
