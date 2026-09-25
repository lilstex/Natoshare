using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;
using Natoshare.Domain.Admin;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Admin;

public class SystemSettingsService : ISystemSettingsService
{
    public const string TrialDaysKey = "TrialDays";

    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IAuditLogger _auditLogger;
    private readonly AppDefaults _defaults;

    public SystemSettingsService(NatoshareDbContext dbContext, IClock clock, IAuditLogger auditLogger, IOptions<AppDefaults> defaults)
    {
        _dbContext = dbContext;
        _clock = clock;
        _auditLogger = auditLogger;
        _defaults = defaults.Value;
    }

    public async Task<IReadOnlyList<SystemSettingDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.SystemSettings.OrderBy(s => s.Key).ToListAsync(cancellationToken);
        return settings.Select(s => new SystemSettingDto(s.Key, s.Value, s.UpdatedAt)).ToList();
    }

    public async Task<SystemSettingDto> SetAsync(string key, PatchSettingRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        var setting = await _dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        var before = setting?.Value;

        if (setting is null)
        {
            setting = new SystemSetting { Key = key };
            _dbContext.SystemSettings.Add(setting);
        }

        setting.Value = request.Value;
        setting.UpdatedAt = _clock.UtcNow;
        setting.UpdatedByAdminUserId = adminUserId;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            adminUserId, "Admin", "SystemSettingUpdated", "SystemSetting", key, null, null,
            before: before, after: setting.Value, cancellationToken: cancellationToken);

        return new SystemSettingDto(setting.Key, setting.Value, setting.UpdatedAt);
    }

    public async Task<int> GetTrialDaysAsync(CancellationToken cancellationToken = default)
    {
        var raw = await _dbContext.SystemSettings
            .Where(s => s.Key == TrialDaysKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return raw is not null && int.TryParse(raw, out var days) ? days : _defaults.TrialDays;
    }
}
