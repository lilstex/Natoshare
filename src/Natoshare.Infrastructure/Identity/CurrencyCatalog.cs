using System.Globalization;
using Natoshare.Application.Common;

namespace Natoshare.Infrastructure.Identity;

// Tells us which currency codes are real, and gives a short list of common ones for
// the onboarding dropdown. We do not keep our own list of every ISO 4217 code, .NET
// already knows the currency tied to every country it supports, so we just ask it.
public class CurrencyCatalog : ICurrencyCatalog
{
    // Built once and reused, working out this list is not free, and it never changes
    // while the app is running.
    private static readonly Lazy<HashSet<string>> KnownCodes = new(() =>
        new HashSet<string>(
            CultureInfo.GetCultures(CultureTypes.SpecificCultures)
                .Select(TryGetCurrencyCode)
                .Where(code => code is not null)!,
            StringComparer.OrdinalIgnoreCase));

    private static string? TryGetCurrencyCode(CultureInfo culture)
    {
        try
        {
            return new RegionInfo(culture.Name).ISOCurrencySymbol;
        }
        catch (ArgumentException)
        {
            // Some cultures are not tied to a real country (for example "en", with no
            // region), RegionInfo cannot work those out, so we just skip them.
            return null;
        }
    }

    public bool IsValidCode(string code) => KnownCodes.Value.Contains(code);

    // A short list to make onboarding easier to use, this does not limit what a user
    // can actually pick, IsValidCode accepts any real ISO 4217 code.
    public IReadOnlyList<CurrencyOption> GetCommonCurrencies() =>
    [
        new CurrencyOption("NGN", "₦", "Nigerian Naira"),
        new CurrencyOption("USD", "$", "US Dollar"),
        new CurrencyOption("EUR", "€", "Euro"),
        new CurrencyOption("GBP", "£", "British Pound"),
        new CurrencyOption("GHS", "₵", "Ghanaian Cedi"),
        new CurrencyOption("KES", "KSh", "Kenyan Shilling"),
        new CurrencyOption("ZAR", "R", "South African Rand"),
        new CurrencyOption("CAD", "$", "Canadian Dollar"),
        new CurrencyOption("AUD", "$", "Australian Dollar"),
        new CurrencyOption("INR", "₹", "Indian Rupee"),
    ];
}
