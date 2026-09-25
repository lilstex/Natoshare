using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Subscriptions;
using Natoshare.Domain.Common;
using Natoshare.Domain.Subscriptions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Identity;

// The stubbed subscription flow: a user asks to go Pro, it sits Pending until an
// admin activates it by hand, since there is no real payment gateway wired up yet
// (docs/00-plan.md section 5, FeatureFlag PaymentsEnabled is always false in v1).
public class SubscriptionService : ISubscriptionService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IEntitlementService _entitlementService;
    private readonly IAuditLogger _auditLogger;

    public SubscriptionService(NatoshareDbContext dbContext, IClock clock, IEntitlementService entitlementService, IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _entitlementService = entitlementService;
        _auditLogger = auditLogger;
    }

    public async Task<SubscriptionStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);

        var activeSubscription = await _dbContext.SubscriptionRecords
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            .OrderByDescending(s => s.ActivatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return new SubscriptionStatusDto(
            entitlements.Plan, activeSubscription?.Status.ToString() ?? "None", entitlements.IsTrial, entitlements.TrialEndsAt,
            activeSubscription?.PeriodEnd);
    }

    public async Task<UpgradeResultDto> UpgradeAsync(Guid userId, UpgradeSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var paymentsEnabled = await _dbContext.FeatureFlags
            .Where(f => f.Key == "PaymentsEnabled")
            .Select(f => f.Enabled)
            .FirstOrDefaultAsync(cancellationToken);

        var record = new SubscriptionRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Plan = PlanTier.Pro,
            BillingCycle = Enum.Parse<BillingCycle>(request.BillingCycle, ignoreCase: true),
            Status = SubscriptionStatus.Pending,
            Reference = $"SUB-{Guid.CreateVersion7():N}"[..16].ToUpperInvariant(),
            RequestedAt = _clock.UtcNow,
        };

        _dbContext.SubscriptionRecords.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Payments are stubbed for the whole of v1, so this branch is the only one
        // that ever actually runs today, the flag exists so a later payments
        // integration has somewhere real to plug in without changing this contract.
        var message = paymentsEnabled
            ? "Your upgrade is being processed."
            : "An admin will activate your upgrade.";

        return new UpgradeResultDto(record.Reference, record.Status.ToString(), message);
    }

    public async Task<IReadOnlyList<SubscriptionRecordDto>> GetHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var records = await _dbContext.SubscriptionRecords
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.RequestedAt)
            .ToListAsync(cancellationToken);

        return records.Select(ToDto).ToList();
    }

    public async Task ActivateAsync(
        Guid adminUserId, string reference, ActivateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var record = await _dbContext.SubscriptionRecords.FirstOrDefaultAsync(s => s.Reference == reference, cancellationToken)
            ?? throw new NotFoundException("We could not find a subscription with that reference.");

        if (record.Status != SubscriptionStatus.Pending)
        {
            throw new ConflictException($"This subscription is already {record.Status.ToString().ToLowerInvariant()}, it cannot be activated again.");
        }

        record.Status = SubscriptionStatus.Active;
        record.ActivatedAt = _clock.UtcNow;
        record.ActivatedByAdminUserId = adminUserId;
        record.PeriodEnd = request.PeriodEnd;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(
            adminUserId, "Admin", "SubscriptionActivated", "SubscriptionRecord", record.Id.ToString(), null, null,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AdminSubscriptionRecordDto>> ListAsync(string? status, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.SubscriptionRecords.Join(
            _dbContext.Users, s => s.UserId, u => u.Id,
            (s, u) => new { Subscription = s, u.Email, u.DisplayName });

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<SubscriptionStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(x => x.Subscription.Status == parsed);
        }

        var rows = await query.OrderByDescending(x => x.Subscription.RequestedAt).ToListAsync(cancellationToken);

        return rows.Select(x => new AdminSubscriptionRecordDto(
            x.Subscription.Id, x.Subscription.UserId, x.Email ?? string.Empty, x.DisplayName,
            x.Subscription.Plan.ToString(), x.Subscription.BillingCycle.ToString(), x.Subscription.Status.ToString(),
            x.Subscription.Reference, x.Subscription.RequestedAt, x.Subscription.ActivatedAt, x.Subscription.PeriodEnd))
            .ToList();
    }

    private static SubscriptionRecordDto ToDto(SubscriptionRecord record) => new(
        record.Id, record.Plan.ToString(), record.BillingCycle.ToString(), record.Status.ToString(), record.Reference,
        record.RequestedAt, record.ActivatedAt, record.PeriodEnd);
}
