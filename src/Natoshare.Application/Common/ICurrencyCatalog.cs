namespace Natoshare.Application.Common;

// One currency a user can pick, shown as an option in the onboarding dropdown.
public record CurrencyOption(string Code, string Symbol, string Name);

// Knows about currencies: which codes are real ISO 4217 codes, and which ones we show
// as quick options during onboarding. Any valid code is accepted, the list of options
// is just a shortlist for a nicer picker, it does not limit what a user can choose.
public interface ICurrencyCatalog
{
    bool IsValidCode(string code);

    IReadOnlyList<CurrencyOption> GetCommonCurrencies();
}
