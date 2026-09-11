using Natoshare.Application.Auth;

namespace Natoshare.Application.Me;

// What GET /me hands back: the user's own profile, plus whether they have finished
// onboarding yet.
public record GetMeResult(UserSummary User, bool OnboardingCompleted);

// Fields you can change about your own profile. Anything left out (null) is left as is.
public record UpdateMeRequest(string? DisplayName, string? TimeZoneId, string? Locale);

public record UpdateCurrencyRequest(string Code, string Symbol);

public record DeleteAccountRequest(string Password, string ConfirmText);

public record ExportStatusResult(string Status, string? Url);
