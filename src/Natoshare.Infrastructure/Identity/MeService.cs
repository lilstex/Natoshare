using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Me;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Identity;

// Everything a logged in user can do to their own profile: see it, change it, change
// their currency, ask for a data export, or delete their account.
public class MeService : IMeService
{
    private readonly UserManager<User> _userManager;
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IAuditLogger _auditLogger;

    public MeService(UserManager<User> userManager, NatoshareDbContext dbContext, IClock clock, IAuditLogger auditLogger)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _clock = clock;
        _auditLogger = auditLogger;
    }

    public async Task<GetMeResult> GetMeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        return await ToResultAsync(user);
    }

    public async Task<GetMeResult> UpdateMeAsync(Guid userId, UpdateMeRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        if (request.DisplayName is not null)
        {
            user.DisplayName = request.DisplayName;
        }

        if (request.TimeZoneId is not null)
        {
            user.TimeZoneId = request.TimeZoneId;
        }

        if (request.Locale is not null)
        {
            user.Locale = request.Locale;
        }

        await _userManager.UpdateAsync(user);

        return await ToResultAsync(user);
    }

    public async Task<GetMeResult> UpdateCurrencyAsync(Guid userId, UpdateCurrencyRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        if (user.CurrencyLocked)
        {
            throw new ConflictException("Your currency is locked because you already have transactions logged.");
        }

        user.CurrencyCode = request.Code.ToUpperInvariant();
        user.CurrencySymbol = request.Symbol;
        await _userManager.UpdateAsync(user);

        return await ToResultAsync(user);
    }

    public async Task<Guid> RequestExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var export = new DataExportRequest
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Status = DataExportStatus.Pending,
            RequestedAt = _clock.UtcNow,
        };

        _dbContext.DataExportRequests.Add(export);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return export.Id;
    }

    public async Task<ExportStatusResult> GetExportStatusAsync(Guid userId, Guid exportId, CancellationToken cancellationToken = default)
    {
        var export = await _dbContext.DataExportRequests
            .FirstOrDefaultAsync(e => e.Id == exportId && e.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that export.");

        return new ExportStatusResult(export.Status.ToString(), export.DownloadUrl);
    }

    public async Task DeleteAccountAsync(Guid userId, DeleteAccountRequest request, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            throw new AuthenticationFailedException("Your password is not correct.");
        }

        user.Status = UserStatus.PendingDeletion;
        user.PendingDeletionRequestedAt = _clock.UtcNow;
        await _userManager.UpdateAsync(user);

        // Sign this account out everywhere right away, they should not have to wait
        // for the cleanup job (that job comes in a later phase) to actually run.
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(user.Id, "User", "AccountDeletionRequested", "User", user.Id.ToString(), ip, null, cancellationToken);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("We could not find your account.");
    }

    private async Task<GetMeResult> ToResultAsync(User user)
    {
        var role = await _userManager.GetPrimaryRoleAsync(user);
        return new GetMeResult(user.ToSummary(role), user.OnboardingCompletedAt is not null);
    }
}
