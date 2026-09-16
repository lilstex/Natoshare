namespace Natoshare.Application.Common;

// Small helper for turning "now" (UTC, from IClock) into "today" in a user's own
// timezone. A Nigerian user and a German user logging an expense at the same instant
// should each get their own local date, not the server's.
public static class UserTime
{
    public static DateOnly TodayFor(string timeZoneId, DateTimeOffset utcNow)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(utcNow, timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }
}
