namespace Natoshare.Domain.Planning;

// Pure date math for a RecurringItem's schedule, kept separate from anything that
// touches a database so the cadence rules can be tested on their own.
public static class RecurringScheduleCalculator
{
    // Where a brand new item's schedule starts: the first date on or after today
    // that matches its cadence. Monthly and BiWeekly both still need their own
    // AdvanceOnce step after this to reach their second occurrence.
    public static DateOnly FirstRunOn(RecurringCadence cadence, int anchorDay, DateOnly today) => cadence switch
    {
        RecurringCadence.Monthly => MonthlyAnchorOnOrAfter(anchorDay, today),
        RecurringCadence.Weekly or RecurringCadence.BiWeekly => NextDayOfWeekOnOrAfter(anchorDay, today),
        _ => throw new ArgumentOutOfRangeException(nameof(cadence)),
    };

    // Where the schedule goes after one occurrence has been handled (posted,
    // reminded, or skipped). Monthly always jumps to next month's anchor day,
    // Weekly adds 7 days, BiWeekly adds 14, so the two-week gap stays fixed to
    // whichever week the item actually started on.
    public static DateOnly AdvanceOnce(RecurringCadence cadence, int anchorDay, DateOnly lastRunOn) => cadence switch
    {
        RecurringCadence.Monthly => MonthlyAnchorDate(NextMonth(lastRunOn), anchorDay),
        RecurringCadence.Weekly => lastRunOn.AddDays(7),
        RecurringCadence.BiWeekly => lastRunOn.AddDays(14),
        _ => throw new ArgumentOutOfRangeException(nameof(cadence)),
    };

    // How much one occurrence is worth, spread evenly across a month, so a Weekly
    // or BiWeekly commitment can be compared side by side with a Monthly one.
    public static decimal NormalizeToMonthly(RecurringCadence cadence, decimal amount) => cadence switch
    {
        RecurringCadence.Monthly => amount,
        RecurringCadence.Weekly => amount * 52m / 12m,
        RecurringCadence.BiWeekly => amount * 26m / 12m,
        _ => throw new ArgumentOutOfRangeException(nameof(cadence)),
    };

    private static DateOnly MonthlyAnchorOnOrAfter(int anchorDay, DateOnly today)
    {
        var thisMonth = MonthlyAnchorDate(today, anchorDay);
        return thisMonth >= today ? thisMonth : MonthlyAnchorDate(NextMonth(today), anchorDay);
    }

    private static DateOnly MonthlyAnchorDate(DateOnly monthOf, int anchorDay)
    {
        var daysInMonth = DateTime.DaysInMonth(monthOf.Year, monthOf.Month);
        var day = anchorDay == RecurringItem.EndOfMonthAnchorDay ? daysInMonth : Math.Min(anchorDay, daysInMonth);
        return new DateOnly(monthOf.Year, monthOf.Month, day);
    }

    private static DateOnly NextMonth(DateOnly date) => date.AddDays(1 - date.Day).AddMonths(1);

    private static DateOnly NextDayOfWeekOnOrAfter(int anchorDayOfWeek, DateOnly from)
    {
        var diff = ((anchorDayOfWeek - (int)from.DayOfWeek) + 7) % 7;
        return from.AddDays(diff);
    }
}
