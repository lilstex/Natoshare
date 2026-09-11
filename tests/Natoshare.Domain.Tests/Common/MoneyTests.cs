using FluentAssertions;
using Natoshare.Domain.Common;
using Xunit;

namespace Natoshare.Domain.Tests.Common;

// These tests check that Money follows the one rule that matters most in Natoshare:
// it can never be negative, and every split of money must add up exactly.
public class MoneyTests
{
    [Fact]
    public void Constructor_throws_when_amount_is_negative()
    {
        // Trying to build negative money should never work, it should blow up instead.
        var act = () => new Money(-1m);

        act.Should().Throw<NegativeMoneyException>();
    }

    [Fact]
    public void Constructor_rounds_to_two_decimal_places()
    {
        var money = new Money(100.005m);

        // Half away from zero, so 100.005 becomes 100.01.
        money.Amount.Should().Be(100.01m);
    }

    [Fact]
    public void Zero_is_zero_amount()
    {
        Money.Zero.Amount.Should().Be(0m);
    }

    [Fact]
    public void Adding_two_money_values_gives_the_total()
    {
        var a = new Money(100m);
        var b = new Money(50m);

        var result = a + b;

        result.Amount.Should().Be(150m);
    }

    [Fact]
    public void TrySubtract_returns_the_leftover_when_there_is_enough_money()
    {
        var available = new Money(100m);

        var (wasEnough, result) = available.TrySubtract(new Money(40m));

        wasEnough.Should().BeTrue();
        result.Amount.Should().Be(60m);
    }

    [Fact]
    public void TrySubtract_does_not_go_negative_when_there_is_not_enough_money()
    {
        var available = new Money(30m);

        var (wasEnough, result) = available.TrySubtract(new Money(50m));

        // We must never see a negative number here, we just get told it was not enough.
        wasEnough.Should().BeFalse();
        result.Should().Be(Money.Zero);
    }

    [Fact]
    public void Deficit_is_zero_when_the_amount_available_covers_what_is_needed()
    {
        var deficit = Money.Deficit(have: new Money(100m), need: new Money(80m));

        deficit.Should().Be(Money.Zero);
    }

    [Fact]
    public void Deficit_is_a_positive_number_showing_how_much_is_missing()
    {
        var deficit = Money.Deficit(have: new Money(80m), need: new Money(95m));

        // 95 needed, only 80 available, so we are short by 15. This 15 is never negative.
        deficit.Amount.Should().Be(15m);
    }

    [Fact]
    public void Allocate_splits_the_amount_so_the_parts_add_up_to_the_exact_total()
    {
        // This matches the Natoshare default categories: Rent 25, Feeding 25,
        // Transportation 15, Utility 10, Subscription 5, Investment 20.
        var income = new Money(500_000m);
        var percentages = new decimal[] { 25m, 25m, 15m, 10m, 5m, 20m };

        var shares = income.Allocate(percentages);

        shares.Select(m => m.Amount).Should().Equal(
            125_000m, 125_000m, 75_000m, 50_000m, 25_000m, 100_000m);

        // The most important check: the parts must add back up to the exact income amount.
        shares.Sum(m => m.Amount).Should().Be(income.Amount);
    }

    [Fact]
    public void Allocate_gives_the_rounding_leftover_to_the_biggest_share()
    {
        // 333,333 split three ways does not divide evenly, so whatever is left over from
        // rounding should land on the biggest share, not just disappear.
        var income = new Money(333_333m);
        var percentages = new decimal[] { 34m, 33m, 33m };

        var shares = income.Allocate(percentages);

        shares.Sum(m => m.Amount).Should().Be(income.Amount);
        shares[0].Amount.Should().BeGreaterThan(shares[1].Amount);
    }

    [Fact]
    public void Max_and_Min_pick_the_correct_side()
    {
        var small = new Money(10m);
        var big = new Money(90m);

        Money.Max(small, big).Should().Be(big);
        Money.Min(small, big).Should().Be(small);
    }
}
