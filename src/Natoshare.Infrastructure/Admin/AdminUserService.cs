using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Admin;
using Natoshare.Application.Auth;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.Maintenance;
using Natoshare.Application.Me;
using Natoshare.Application.Subscriptions;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Domain.Subscriptions;
using Natoshare.Infrastructure.Identity;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Admin;

// The Users section of the admin app: read-first (list and detail show everything
// the team needs without touching anything), and every action that does change
// something is audited with a before/after line, see docs/04-admin-app.md section
// 2.2 and its "everything is audited" principle.
public class AdminUserService : IAdminUserService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly IClock _clock;
    private readonly IAuditLogger _auditLogger;
    private readonly IEntitlementService _entitlementService;
    private readonly IBalanceService _balanceService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IMaintenanceJobs _maintenanceJobs;
    private readonly IAuthService _authService;
    private readonly IMeService _meService;

    public AdminUserService(
        NatoshareDbContext dbContext,
        UserManager<User> userManager,
        IClock clock,
        IAuditLogger auditLogger,
        IEntitlementService entitlementService,
        IBalanceService balanceService,
        ISubscriptionService subscriptionService,
        IMaintenanceJobs maintenanceJobs,
        IAuthService authService,
        IMeService meService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _clock = clock;
        _auditLogger = auditLogger;
        _entitlementService = entitlementService;
        _balanceService = balanceService;
        _subscriptionService = subscriptionService;
        _maintenanceJobs = maintenanceJobs;
        _authService = authService;
        _meService = meService;
    }

    public async Task<AdminUserListResult> ListAsync(
        string? query, string? status, string? plan, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Clamped server side, not just trusted from the query string, so a
        // careless or malicious ?pageSize=999999 cannot force a full table scan.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var usersQuery = _dbContext.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim().ToLowerInvariant();
            usersQuery = usersQuery.Where(u => u.Email!.ToLower().Contains(term) || u.DisplayName.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            usersQuery = usersQuery.Where(u => u.Status == parsedStatus);
        }

        var totalCount = await usersQuery.CountAsync(cancellationToken);

        var users = await usersQuery
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<AdminUserListItemDto>();
        foreach (var user in users)
        {
            var role = await _userManager.GetPrimaryRoleAsync(user);
            var entitlements = await _entitlementService.ResolveAsync(user.Id, cancellationToken);

            if (!string.IsNullOrWhiteSpace(plan) && !string.Equals(entitlements.Plan, plan, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new AdminUserListItemDto(
                user.Id, user.Email ?? string.Empty, user.DisplayName, user.Status.ToString(), role,
                entitlements.Plan, entitlements.IsTrial, user.CreatedAt));
        }

        return new AdminUserListResult(items, totalCount, page, pageSize);
    }

    public async Task<AdminUserDetailDto> GetDetailAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        var role = await _userManager.GetPrimaryRoleAsync(user);
        var entitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
        var currentMonth = await _balanceService.GetCurrentAsync(userId, cancellationToken);
        var subscriptionHistory = await _subscriptionService.GetHistoryAsync(userId, cancellationToken);

        var recentActivityRows = await _dbContext.AuditEvents
            .Where(a => a.ActorUserId == userId || (a.EntityType == "User" && a.EntityId == userId.ToString()))
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var deficitResolutionCount = await _dbContext.DeficitResolutions.CountAsync(d => d.UserId == userId, cancellationToken);
        var configVersionCount = await _dbContext.AllocationConfigVersions.CountAsync(v => v.UserId == userId, cancellationToken);
        var unreadNotificationCount = await _dbContext.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken);
        var alertPreferenceCount = await _dbContext.AlertPreferences.CountAsync(p => p.UserId == userId, cancellationToken);

        return new AdminUserDetailDto(
            user.Id, user.Email ?? string.Empty, user.DisplayName, user.Status.ToString(), role, user.TimeZoneId, user.CurrencyCode,
            user.CreatedAt, user.TrialEndsAt, user.OnboardingCompletedAt, entitlements, currentMonth, subscriptionHistory,
            recentActivityRows.Select(ToDto).ToList(), deficitResolutionCount, configVersionCount, unreadNotificationCount,
            alertPreferenceCount);
    }

    public async Task SuspendAsync(Guid adminUserId, Guid userId, SuspendUserRequest request, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        var before = $"Status: {user.Status}";

        user.Status = UserStatus.Suspended;
        await _userManager.UpdateAsync(user);
        await RevokeAllRefreshTokensAsync(userId, cancellationToken);

        await _auditLogger.LogAsync(
            adminUserId, "Admin", "UserSuspended", "User", userId.ToString(), ip, null,
            before: before, after: $"Status: Suspended, Reason: {request.Reason}", cancellationToken: cancellationToken);
    }

    public async Task ReactivateAsync(Guid adminUserId, Guid userId, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        var before = $"Status: {user.Status}";

        user.Status = UserStatus.Active;
        await _userManager.UpdateAsync(user);

        await _auditLogger.LogAsync(
            adminUserId, "Admin", "UserReactivated", "User", userId.ToString(), ip, null,
            before: before, after: "Status: Active", cancellationToken: cancellationToken);
    }

    public async Task<AdminResetPasswordResult> ResetPasswordAsync(Guid adminUserId, Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        var token = await _authService.AdminGeneratePasswordResetTokenAsync(userId, adminUserId, cancellationToken);
        await RevokeAllRefreshTokensAsync(userId, cancellationToken);

        return new AdminResetPasswordResult(token);
    }

    public async Task AdjustPlanAsync(Guid adminUserId, Guid userId, AdjustPlanRequest request, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        var beforeEntitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
        var before = $"Plan: {beforeEntitlements.Plan}, TrialEndsAt: {user.TrialEndsAt:u}";

        if (request.TrialEndsAt is not null)
        {
            user.TrialEndsAt = request.TrialEndsAt.Value;
            await _userManager.UpdateAsync(user);
        }

        if (request.Plan is not null)
        {
            // We do not touch existing Pending/Active records here, this is a direct
            // admin override, not a normal upgrade going through the request/activate
            // flow (that flow still exists for the self-service path).
            if (string.Equals(request.Plan, "Pro", StringComparison.OrdinalIgnoreCase))
            {
                _dbContext.SubscriptionRecords.Add(new SubscriptionRecord
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    Plan = PlanTier.Pro,
                    BillingCycle = BillingCycle.Monthly,
                    Status = SubscriptionStatus.Active,
                    Reference = $"ADM-{Guid.CreateVersion7():N}"[..16].ToUpperInvariant(),
                    RequestedAt = _clock.UtcNow,
                    ActivatedAt = _clock.UtcNow,
                    ActivatedByAdminUserId = adminUserId,
                });
            }
            else
            {
                var activeRecords = await _dbContext.SubscriptionRecords
                    .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
                    .ToListAsync(cancellationToken);

                foreach (var record in activeRecords)
                {
                    record.Status = SubscriptionStatus.Expired;
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var afterEntitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
        await _auditLogger.LogAsync(
            adminUserId, "Admin", "UserPlanAdjusted", "User", userId.ToString(), ip, null,
            before: before, after: $"Plan: {afterEntitlements.Plan}, TrialEndsAt: {user.TrialEndsAt:u}",
            cancellationToken: cancellationToken);
    }

    public async Task<AdminExportResult> ExportAsync(Guid adminUserId, Guid userId, string? ip, CancellationToken cancellationToken = default)
    {
        await GetUserOrThrowAsync(userId);
        var exportId = await _meService.RequestExportAsync(userId, cancellationToken);

        await _auditLogger.LogAsync(
            adminUserId, "Admin", "AdminRequestedExport", "User", userId.ToString(), ip, null,
            cancellationToken: cancellationToken);

        return new AdminExportResult(exportId);
    }

    public async Task HardDeleteAsync(Guid adminUserId, Guid userId, HardDeleteUserRequest request, string? ip, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);

        if (!string.Equals(request.ConfirmText, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationFailedException(
                new Dictionary<string, string[]> { ["confirmText"] = ["Type the account's email exactly to confirm this cannot be undone."] });
        }

        // Written before the purge, an audit trail of what happened should outlive
        // the account it is about (the same reasoning MaintenanceJobs' own purge
        // uses for the daily grace-period path).
        await _auditLogger.LogAsync(
            adminUserId, "Admin", "UserHardDeleted", "User", userId.ToString(), ip, null,
            before: $"Email: {user.Email}, Status: {user.Status}", after: "Deleted", cancellationToken: cancellationToken);

        await _maintenanceJobs.PurgeUserImmediatelyAsync(userId, cancellationToken);
    }

    public async Task<RecomputeBalancesResult> RecomputeBalancesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetUserOrThrowAsync(userId);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);

        var budgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == today.Year && m.Month == today.Month, cancellationToken);

        if (budgetMonth is null)
        {
            return new RecomputeBalancesResult(today.Year, today.Month, []);
        }

        var categoryIds = budgetMonth.CategoryMonths.Select(cm => cm.CategoryId).ToList();
        var categoryNames = await _dbContext.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);

        var diffs = new List<RecomputeDiffItemDto>();
        foreach (var categoryMonth in budgetMonth.CategoryMonths)
        {
            var recomputedSpent = await _dbContext.Expenses
                .Where(e => e.BudgetMonthId == budgetMonth.Id && e.CategoryId == categoryMonth.CategoryId)
                .SumAsync(e => (decimal?)e.Amount.Amount, cancellationToken) ?? 0m;

            var storedSpent = categoryMonth.SpentAmount.Amount;

            diffs.Add(new RecomputeDiffItemDto(
                categoryMonth.CategoryId,
                categoryNames.GetValueOrDefault(categoryMonth.CategoryId, "(deleted category)"),
                storedSpent,
                recomputedSpent,
                storedSpent != recomputedSpent));
        }

        return new RecomputeBalancesResult(today.Year, today.Month, diffs);
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await _dbContext.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken);
        var now = _clock.UtcNow;
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetUserOrThrowAsync(Guid userId)
    {
        return await _userManager.FindByIdAsync(userId.ToString()) ?? throw new NotFoundException("We could not find that user.");
    }

    private static AdminAuditEventDto ToDto(Natoshare.Domain.Audit.AuditEvent e) => new(
        e.Id, e.ActorUserId, e.ActorRole, e.Action, e.EntityType, e.EntityId, AuditJson.Decode(e.Before), AuditJson.Decode(e.After), e.Ip, e.CreatedAt);
}
