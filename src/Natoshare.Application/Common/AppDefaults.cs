namespace Natoshare.Application.Common;

// The defaults we give a brand new account, before the user goes through onboarding
// (Phase 2) and changes them. These come from the "App" section of appsettings.
public class AppDefaults
{
    public string DefaultTimeZone { get; set; } = "Africa/Lagos";
    public string DefaultLocale { get; set; } = "en-NG";
    public string DefaultCurrencyCode { get; set; } = "NGN";
    public string DefaultCurrencySymbol { get; set; } = "₦";
    public int TrialDays { get; set; } = 30;
}
