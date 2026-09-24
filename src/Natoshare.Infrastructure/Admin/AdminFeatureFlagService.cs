using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;
using Natoshare.Domain.Common;
using Natoshare.Domain.Subscriptions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Admin;

public class AdminFeatureFlagService : IAdminFeatureFlagService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IAuditLogger _auditLogger;

    public AdminFeatureFlagService(NatoshareDbContext dbContext, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<AdminFeatureFlagDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var flags = await _dbContext.FeatureFlags.OrderBy(f => f.Key).ToListAsync(cancellationToken);
        return flags.Select(f => new AdminFeatureFlagDto(f.Key, f.Enabled)).ToList();
    }

    public async Task<AdminFeatureFlagDto> UpdateAsync(string key, PatchFeatureFlagRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var flag = await _dbContext.FeatureFlags.FirstOrDefaultAsync(f => f.Key == key, cancellationToken)
            ?? throw new NotFoundException($"There is no feature flag called '{key}'.");

        var before = $"Enabled: {flag.Enabled}";
        flag.Enabled = request.Enabled;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            adminUserId, "Admin", "FeatureFlagUpdated", "FeatureFlag", key, null, null,
            before: before, after: $"Enabled: {flag.Enabled}", cancellationToken: cancellationToken);

        return new AdminFeatureFlagDto(flag.Key, flag.Enabled);
    }
}
