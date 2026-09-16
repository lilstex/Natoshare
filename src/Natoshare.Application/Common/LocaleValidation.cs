using System.Globalization;

namespace Natoshare.Application.Common;

// Shared checks for timezone and locale strings, used by any validator that takes
// either one (there is more than one now: profile updates and onboarding).
public static class LocaleValidation
{
    public static bool IsKnownTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    // CultureInfo.GetCultureInfo("anything-shaped-like-a-tag") almost never throws,
    // .NET treats most made up tags as a valid "custom" culture instead of rejecting
    // them. So instead we check the tag is in the real list of locales .NET ships,
    // which is what actually tells us it is a locale someone can use.
    private static readonly Lazy<HashSet<string>> KnownLocales = new(() =>
        new HashSet<string>(
            CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase));

    public static bool IsKnownLocale(string locale) => KnownLocales.Value.Contains(locale);
}
