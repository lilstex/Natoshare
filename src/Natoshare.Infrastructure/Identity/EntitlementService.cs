using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Domain.Common;
using Natoshare.Domain.Subscriptions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Identity;

public class EntitlementService : IEntitlementService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;

    public EntitlementService(NatoshareDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<PlanEntitlements> ResolveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.Where(u => u.Id == userId).Select(u => new { u.TrialEndsAt }).FirstAsync(cancellationToken);
        var now = _clock.UtcNow;
        var isTrial = now < user.TrialEndsAt;

        var hasActiveSubscription = isTrial || await _dbContext.SubscriptionRecords.AnyAsync(
            s => s.UserId == userId && s.Status == SubscriptionStatus.Active && (s.PeriodEnd == null || s.PeriodEnd > now),
            cancellationToken);

        var plan = hasActiveSubscription ? PlanTier.Pro : PlanTier.Free;

        var config = await _dbContext.PlanConfigs.FirstOrDefaultAsync(p => p.Plan == plan, cancellationToken)
            ?? throw new InvalidOperationException($"No PlanConfig seeded for {plan}, has ReferenceDataSeeder run?");

        return new PlanEntitlements(
            plan.ToString(), isTrial, user.TrialEndsAt, config.MaxCategories, config.HistoryWindowDays,
            config.SinkingFundEnabled, config.DeficitCoverFromSavingsEnabled, config.RecurringItemsEnabled, config.ExportEnabled);
    }

    public async Task EnsureEntitledAsync(Guid userId, string featureName, CancellationToken cancellationToken = default)
    {
        var entitlements = await ResolveAsync(userId, cancellationToken);
        var allowed = featureName switch
        {
            "Recurring items" => entitlements.Recurring,
            "Report export" => entitlements.Export,
            _ => true,
        };

        if (!allowed)
        {
            throw new UpgradeRequiredException($"{featureName} needs a Pro plan or an active trial.");
        }
    }

    public async Task<IReadOnlySet<Guid>> GetLockedCategoryIdsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entitlements = await ResolveAsync(userId, cancellationToken);
        if (entitlements.MaxCategories is null)
        {
            return new HashSet<Guid>();
        }

        // The same SortOrder a user already sees on their categories screen decides
        // which ones stay unlocked, so the answer never shuffles around between
        // calls, only the count in front of it changes as categories are added,
        // reordered or archived.
        var activeCategoryIds = await _dbContext.Categories
            .Where(c => c.UserId == userId && !c.IsArchived)
            .OrderBy(c => c.SortOrder)
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);

        return activeCategoryIds.Skip(entitlements.MaxCategories.Value).ToHashSet();
    }
}
