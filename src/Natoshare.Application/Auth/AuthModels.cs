namespace Natoshare.Application.Auth;

// What someone sends us to create a new account.
public record SignupRequest(string Email, string Password, string DisplayName);

public record LoginRequest(string Email, string Password);

// Used by both /auth/refresh and /auth/logout, both only need the refresh token.
public record RefreshRequest(string RefreshToken);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string ResetToken, string NewPassword);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// A short, safe view of a user we can hand back over the API. No password hash, no
// internal ids beyond the user's own, nothing sensitive.
public record UserSummary(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string CurrencyCode,
    string CurrencySymbol,
    string TimeZoneId,
    string Locale,
    DateTimeOffset TrialEndsAt);

// What we hand back after a successful signup, login, or token refresh.
public record AuthResult(UserSummary User, string AccessToken, string RefreshToken);
