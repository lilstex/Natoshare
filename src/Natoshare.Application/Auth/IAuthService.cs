namespace Natoshare.Application.Auth;

// Everything to do with signing up, logging in, and staying logged in.
public interface IAuthService
{
    Task<AuthResult> SignupAsync(SignupRequest request, string? ip, CancellationToken cancellationToken = default);

    Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken cancellationToken = default);

    Task<AuthResult> RefreshAsync(string refreshToken, string? ip, CancellationToken cancellationToken = default);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);

    // Starts a password reset. In development this hands back the raw reset token so
    // you can test the flow without an email system. In production it returns null,
    // an admin has to fetch the token for the user instead (see Phase 10).
    Task<string?> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);

    // An admin triggering a reset on someone's behalf (Phase 10), for when a user is
    // locked out and cannot use the normal ForgotPassword flow themselves. Unlike
    // ForgotPasswordAsync, this always hands back the raw token, an admin is trusted
    // to pass it on to the user through whatever support channel they used to ask.
    Task<string> AdminGeneratePasswordResetTokenAsync(Guid userId, Guid adminUserId, CancellationToken cancellationToken = default);
}
