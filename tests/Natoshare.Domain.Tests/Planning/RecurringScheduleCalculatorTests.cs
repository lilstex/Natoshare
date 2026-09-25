using FluentAssertions;
using Natoshare.Domain.Planning;
using Xunit;

namespace Natoshare.Domain.Tests.Planning;

public class RecurringScheduleCalculatorTests
{
    [Fact]
    public void Monthly_first_run_lands_on_the_anchor_day_this_month_when_it_has_not_passed_yet()
    {
        var today = new DateOnly(2026, 9, 10);
        var result = RecurringScheduleCalculator.FirstRunOn(RecurringCadence.Monthly, 15, today);

        result.Should().Be(new DateOnly(2026, 9, 15));
    }

    [Fact]
    public void Monthly_first_run_rolls_to_next_month_when_the_anchor_day_already_passed()
    {
        var today = new DateOnly(2026, 9, 20);
        var result = RecurringScheduleCalculator.FirstRunOn(RecurringCadence.Monthly, 15, today);

        result.Should().Be(new DateOnly(2026, 10, 15));
    }

    [Fact]
    public void Monthly_end_of_month_anchor_lands_on_the_real_last_day_of_a_30_day_month()
    {
        var today = new DateOnly(2026, 9, 1);
        var result = RecurringScheduleCalculator.FirstRunOn(RecurringCadence.Monthly, RecurringItem.EndOfMonthAnchorDay, today);

        result.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Monthly_end_of_month_anchor_lands_on_the_28th_in_february_on_a_non_leap_year()
    {
        var today = new DateOnly(2026, 2, 1);
        var result = RecurringScheduleCalculator.AdvanceOnce(RecurringCadence.Monthly, RecurringItem.EndOfMonthAnchorDay, new DateOnly(2026, 1, 31));

        result.Should().Be(new DateOnly(2026, 2, 28));
    }

    [Fact]
    public void Monthly_advance_clamps_an_anchor_day_that_does_not_exist_in_a_shorter_month()
    {
        // AnchorDay only ever validates up to 28 through the API (see
        // PlanningValidators), but AdvanceOnce is still safe against a bigger value,
        // clamping to the real last day instead of throwing, since August's 31st
        // rolling into a 30-day September is exactly the kind of month-end edge case
        // this calculator has to survive.
        var result = RecurringScheduleCalculator.AdvanceOnce(RecurringCadence.Monthly, 31, new DateOnly(2026, 8, 31));

        result.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Monthly_advance_always_jumps_to_next_month_even_if_called_early()
    {
        var result = RecurringScheduleCalculator.AdvanceOnce(RecurringCadence.Monthly, 15, new DateOnly(2026, 9, 15));

        result.Should().Be(new DateOnly(2026, 10, 15));
    }

    [Fact]
    public void Weekly_first_run_is_today_when_today_already_matches_the_anchor_day_of_week()
    {
        var wednesday = new DateOnly(2026, 9, 16);
        wednesday.DayOfWeek.Should().Be(DayOfWeek.Wednesday);

        var result = RecurringScheduleCalculator.FirstRunOn(RecurringCadence.Weekly, (int)DayOfWeek.Wednesday, wednesday);

        result.Should().Be(wednesday);
    }

    [Fact]
    public void Weekly_first_run_rolls_forward_to_the_next_matching_day_of_week()
    {
        var wednesday = new DateOnly(2026, 9, 16);
        var result = RecurringScheduleCalculator.FirstRunOn(RecurringCadence.Weekly, (int)DayOfWeek.Friday, wednesday);

        result.Should().Be(new DateOnly(2026, 9, 18));
    }

    [Fact]
    public void Weekly_advance_always_adds_exactly_seven_days()
    {
        var result = RecurringScheduleCalculator.AdvanceOnce(RecurringCadence.Weekly, (int)DayOfWeek.Friday, new DateOnly(2026, 9, 18));

        result.Should().Be(new DateOnly(2026, 9, 25));
    }

    [Fact]
    public void BiWeekly_advance_always_adds_exactly_fourteen_days()
    {
        var result = RecurringScheduleCalculator.AdvanceOnce(RecurringCadence.BiWeekly, (int)DayOfWeek.Friday, new DateOnly(2026, 9, 18));

        result.Should().Be(new DateOnly(2026, 10, 2));
    }

    [Fact]
    public void Monthly_normalises_to_its_own_amount()
    {
        RecurringScheduleCalculator.NormalizeToMonthly(RecurringCadence.Monthly, 1000m).Should().Be(1000m);
    }

    [Fact]
    public void Weekly_normalises_to_roughly_four_and_a_third_times_its_amount()
    {
        var result = RecurringScheduleCalculator.NormalizeToMonthly(RecurringCadence.Weekly, 100m);

        result.Should().BeApproximately(433.33m, 0.01m);
    }

    [Fact]
    public void BiWeekly_normalises_to_roughly_two_and_one_sixth_times_its_amount()
    {
        var result = RecurringScheduleCalculator.NormalizeToMonthly(RecurringCadence.BiWeekly, 100m);

        result.Should().BeApproximately(216.67m, 0.01m);
    }
}
