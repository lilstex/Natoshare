using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Natoshare.Application.Auth;
using Natoshare.Application.Common;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Domain.Notifications;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Identity;

// Handles signing up, logging in, staying logged in with refresh tokens, and
// resetting or changing a password.
public class AuthService : IAuthService
{
    private const string DefaultRole = "User";

    private readonly UserManager<User> _userManager;
    private readonly NatoshareDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IClock _clock;
    private readonly IAuditLogger _auditLogger;
    private readonly AppDefaults _defaults;
    private readonly IHostEnvironment _environment;

    public AuthService(
        UserManager<User> userManager,
        NatoshareDbContext dbContext,
        ITokenService tokenService,
        IClock clock,
        IAuditLogger auditLogger,
        IOptions<AppDefaults> defaults,
        IHostEnvironment environment)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _tokenService = tokenService;
        _clock = clock;
        _auditLogger = auditLogger;
        _defaults = defaults.Value;
        _environment = environment;
    }

    public async Task<AuthResult> SignupAsync(SignupRequest request, string? ip, CancellationToken cancellationToken = default)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var now = _clock.UtcNow;
        var user = new User
        {
            Id = Guid.CreateVersion7(),
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
            CurrencyCode = _defaults.DefaultCurrencyCode,
            CurrencySymbol = _defaults.DefaultCurrencySymbol,
            TimeZoneId = _defaults.DefaultTimeZone,
            Locale = _defaults.DefaultLocale,
            TrialEndsAt = now.AddDays(_defaults.TrialDays),
            CreatedAt = now,
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            throw ToValidationException(createResult);
        }

        await _userManager.AddToRoleAsync(user, DefaultRole);
        await SeedDefaultCategoriesAsync(user.Id, now, cancellationToken);
        await SeedDefaultAlertPreferencesAsync(user.Id, cancellationToken);

        var result = await IssueTokensAsync(user, DefaultRole, ip, cancellationToken);

        await _auditLogger.LogAsync(user.Id, DefaultRole, "UserSignedUp", "User", user.Id.ToString(), ip, null, cancellationToken);

        return result;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new AuthenticationFailedException("Your email or password is not correct.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new AuthenticationFailedException("This account is not active. Contact support if you think this is a mistake.");
        }

        var role = await _userManager.GetPrimaryRoleAsync(user);
        var result = await IssueTokensAsync(user, role, ip, cancellationToken);

        await _auditLogger.LogAsync(user.Id, role, "UserLoggedIn", "User", user.Id.ToString(), ip, null, cancellationToken);

        return result;
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, string? ip, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.Hash(refreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        var now = _clock.UtcNow;
        if (stored is null || !stored.IsActive(now))
        {
            throw new AuthenticationFailedException("This session has expired, please log in again.");
        }

        var user = await _userManager.FindByIdAsync(stored.UserId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            throw new AuthenticationFailedException("This session has expired, please log in again.");
        }

        var role = await _userManager.GetPrimaryRoleAsync(user);
        var newRefresh = _tokenService.CreateRefreshToken();

        // We rotate the token: the old one is marked as used and points at the new
        // one. If the old one is ever presented again, we know it must be a copy that
        // leaked, not the real user.
        stored.RevokedAt = now;
        stored.ReplacedByTokenHash = newRefresh.TokenHash;

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = newRefresh.TokenHash,
            ExpiresAt = newRefresh.ExpiresAt,
            CreatedByIp = ip,
            CreatedAt = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = _tokenService.CreateAccessToken(user, role);

        return new AuthResult(user.ToSummary(role), accessToken, newRefresh.RawToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var tokenHash = _tokenService.Hash(refreshToken);
        var stored = await _dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            // Logging out twice, or with a token we never issued, is not an error,
            // the thing the caller wanted (being logged out) is already true.
            return;
        }

        stored.RevokedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(stored.UserId, "User", "UserLoggedOut", "User", stored.UserId.ToString(), null, null, cancellationToken);
    }

    public async Task<string?> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            // We never say whether the email exists or not, that would let someone
            // check who has an account here. We just quietly do nothing instead.
            return null;
        }

        var rawToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
        var now = _clock.UtcNow;

        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = _tokenService.Hash(rawToken),
            ExpiresAt = now.AddHours(1),
            CreatedAt = now,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogAsync(user.Id, "User", "PasswordResetRequested", "User", user.Id.ToString(), null, null, cancellationToken);

        // There is no email system yet, see docs/01-domain-model.md. In development we
        // hand the raw code straight back so the flow can be tested end to end. In
        // production nobody gets it here, an admin looks it up instead (Phase 10).
        return _environment.IsProduction() ? null : rawToken;
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            throw new AuthenticationFailedException("This reset code is not valid or has expired.");
        }

        var tokenHash = _tokenService.Hash(request.ResetToken);
        var now = _clock.UtcNow;
        var storedToken = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.TokenHash == tokenHash)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (storedToken is null || !storedToken.IsUsable(now))
        {
            throw new AuthenticationFailedException("This reset code is not valid or has expired.");
        }

        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            throw ToValidationException(removeResult);
        }

        var addResult = await _userManager.AddPasswordAsync(user, request.NewPassword);
        if (!addResult.Succeeded)
        {
            throw ToValidationException(addResult);
        }

        storedToken.UsedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(user.Id, "User", "PasswordResetCompleted", "User", user.Id.ToString(), null, null, cancellationToken);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("We could not find your account.");

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            throw ToValidationException(result);
        }

        await _auditLogger.LogAsync(user.Id, "User", "PasswordChanged", "User", user.Id.ToString(), null, null, cancellationToken);
    }

    // Gives a brand new account the default set of categories (Rent, Feeding, and so
    // on), so onboarding always has something to start from. They do not have a
    // percentage yet, that only happens once onboarding creates the first allocation
    // version.
    private async Task SeedDefaultCategoriesAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var categories = DefaultCategories.Items.Select((item, index) => new Category
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = item.Name,
            Kind = item.Kind,
            SortOrder = index,
            CreatedAt = now,
        });

        _dbContext.Categories.AddRange(categories);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Gives a brand new account sensible alert settings from day one, so pacing and
    // deficit alerts already work without a trip to a settings page first.
    private async Task SeedDefaultAlertPreferencesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var preferences = DefaultAlertPreferences.Items.Select(item => new AlertPreference
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = item.Kind,
            Enabled = true,
            ThresholdPercent = item.ThresholdPercent,
            LeadDays = item.LeadDays,
        });

        _dbContext.AlertPreferences.AddRange(preferences);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResult> IssueTokensAsync(User user, string role, string? ip, CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.CreateAccessToken(user, role);
        var refresh = _tokenService.CreateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            TokenHash = refresh.TokenHash,
            ExpiresAt = refresh.ExpiresAt,
            CreatedByIp = ip,
            CreatedAt = _clock.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AuthResult(user.ToSummary(role), accessToken, refresh.RawToken);
    }

    // Identity has its own rules (password too short, email already taken, and so
    // on), we turn those into the same shape our own validation errors use, so the
    // frontend only has to handle one error format.
    private static ValidationFailedException ToValidationException(IdentityResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.Code)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        return new ValidationFailedException(errors);
    }
}
