using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Auth;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers;

// Signing up, logging in, staying logged in, and dealing with a forgotten or changed
// password.
[Route("api/v1/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<SignupRequest> _signupValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshRequest> _refreshValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;
    private readonly IValidator<ChangePasswordRequest> _changePasswordValidator;

    public AuthController(
        IAuthService authService,
        ICurrentUser currentUser,
        IValidator<SignupRequest> signupValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshRequest> refreshValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator,
        IValidator<ChangePasswordRequest> changePasswordValidator)
    {
        _authService = authService;
        _currentUser = currentUser;
        _signupValidator = signupValidator;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _changePasswordValidator = changePasswordValidator;
    }

    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Signup(SignupRequest request, CancellationToken cancellationToken)
    {
        await _signupValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _authService.SignupAsync(request, ClientIp, cancellationToken);
        return Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        await _loginValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _authService.LoginAsync(request, ClientIp, cancellationToken);
        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResult>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        await _refreshValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _authService.RefreshAsync(request.RefreshToken, ClientIp, cancellationToken);
        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize(Policy = "RequireUser")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken cancellationToken)
    {
        await _refreshValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _forgotPasswordValidator.ValidateOrThrowAsync(request, cancellationToken);
        var devOnlyResetToken = await _authService.ForgotPasswordAsync(request, cancellationToken);

        // devOnlyResetToken is only ever filled in outside production, see
        // AuthService.ForgotPasswordAsync. In production this is always null, and an
        // admin has to look the code up instead (Phase 10).
        return Accepted(new { resetToken = devOnlyResetToken });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _resetPasswordValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize(Policy = "RequireUser")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _changePasswordValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _authService.ChangePasswordAsync(_currentUser.UserId, request, cancellationToken);
        return NoContent();
    }
}
