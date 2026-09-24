using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;
using Natoshare.Domain.Common;
using Natoshare.Domain.Subscriptions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Admin;

public class AdminPlanService : IAdminPlanService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IAuditLogger _auditLogger;

    public AdminPlanService(NatoshareDbContext dbContext, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<AdminPlanConfigDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _dbContext.PlanConfigs.OrderBy(p => p.Plan).ToListAsync(cancellationToken);
        return configs.Select(ToDto).ToList();
    }

    public async Task<AdminPlanConfigDto> UpdateAsync(string plan, PatchPlanConfigRequest request, Guid adminUserId, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<PlanTier>(plan, ignoreCase: true, out var planTier))
        {
            throw new NotFoundException($"There is no plan called '{plan}'.");
        }

        var config = await _dbContext.PlanConfigs.FirstOrDefaultAsync(p => p.Plan == planTier, cancellationToken)
            ?? throw new NotFoundException($"There is no plan called '{plan}'.");

        var before = ToDto(config);

        config.MaxCategories = request.MaxCategories;
        config.HistoryWindowDays = request.HistoryWindowDays;
        config.SinkingFundEnabled = request.SinkingFund;
        config.DeficitCoverFromSavingsEnabled = request.DeficitCoverFromSavings;
        config.RecurringItemsEnabled = request.Recurring;
        config.ExportEnabled = request.Export;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var after = ToDto(config);
        await _auditLogger.LogAsync(
            adminUserId, "Admin", "PlanConfigUpdated", "PlanConfig", plan, null, null,
            before: before.ToString(), after: after.ToString(), cancellationToken: cancellationToken);

        return after;
    }

    private static AdminPlanConfigDto ToDto(PlanConfig config) => new(
        config.Plan.ToString(), config.MaxCategories, config.HistoryWindowDays, config.SinkingFundEnabled,
        config.DeficitCoverFromSavingsEnabled, config.RecurringItemsEnabled, config.ExportEnabled);
}
