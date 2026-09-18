using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Transactions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// Covering a category's shortfall right now, from somewhere that actually has the
// money. Phase 3 only wires up the three methods that move real money immediately
// (OwnSavings, OtherCategorySavings, FlexiblePool), carrying a deficit to next month
// is a month-close decision that lands in Phase 5.
public class DeficitService : IDeficitService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IReallocationService _reallocationService;
    private readonly IAuditLogger _auditLogger;
    private readonly IEntitlementService _entitlementService;

    public DeficitService(
        NatoshareDbContext dbContext,
        IClock clock,
        ILedgerService ledgerService,
        IReallocationService reallocationService,
        IAuditLogger auditLogger,
        IEntitlementService entitlementService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _reallocationService = reallocationService;
        _auditLogger = auditLogger;
        _entitlementService = entitlementService;
    }

    public async Task<IReadOnlyList<DeficitListItemDto>> ListAsync(
        Guid userId, int? year, int? month, string? status, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var targetYear = year ?? today.Year;
        var targetMonth = month ?? today.Month;

        var budgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == targetYear && m.Month == targetMonth, cancellationToken);

        if (budgetMonth is null)
        {
            return [];
        }

        var categories = await _dbContext.Categories.Where(c => c.UserId == userId).ToDictionaryAsync(c => c.Id, cancellationToken);
        var entitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
        var results = new List<DeficitListItemDto>();

        foreach (var categoryMonth in budgetMonth.CategoryMonths)
        {
            var deficit = categoryMonth.Deficit();
            var hasOpenDeficit = deficit > Money.Zero;
            var hasResolvedHistory = await _dbContext.DeficitResolutions
                .AnyAsync(d => d.BudgetMonthId == budgetMonth.Id && d.CategoryId == categoryMonth.CategoryId, cancellationToken);

            var include = status switch
            {
                "open" => hasOpenDeficit,
                "resolved" => hasResolvedHistory,
                _ => hasOpenDeficit || hasResolvedHistory,
            };

            if (!include)
            {
                continue;
            }

            categories.TryGetValue(categoryMonth.CategoryId, out var category);
            var sources = await SuggestedSourcesAsync(userId, categoryMonth.CategoryId, categories, entitlements.DeficitCoverFromSavings, cancellationToken);

            results.Add(new DeficitListItemDto(
                categoryMonth.CategoryId, category?.Name ?? "(deleted category)", deficit.Amount, categoryMonth.CarriedInDeficit.Amount, sources));
        }

        return results;
    }

    public async Task<DeficitResolutionDto> ResolveAsync(Guid userId, ResolveDeficitRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);

        if (today.Year != request.Year || today.Month != request.Month)
        {
            throw new ConflictException("You can only resolve a deficit for the current month.");
        }

        var budgetMonth = await _dbContext.BudgetMonths
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == request.Year && m.Month == request.Month, cancellationToken)
            ?? throw new NotFoundException("We could not find that month.");

        var categoryMonth = await _dbContext.CategoryMonths
            .FirstOrDefaultAsync(cm => cm.BudgetMonthId == budgetMonth.Id && cm.CategoryId == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("We could not find that category for this month.");

        if (categoryMonth.Deficit() == Money.Zero)
        {
            throw new ConflictException("This category is not currently in deficit.");
        }

        var method = Enum.Parse<DeficitResolutionMethod>(request.Method);
        if (method is DeficitResolutionMethod.OwnSavings or DeficitResolutionMethod.OtherCategorySavings)
        {
            var entitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
            if (!entitlements.DeficitCoverFromSavings)
            {
                throw new UpgradeRequiredException(
                    "Covering a deficit from savings needs a Pro plan or an active trial, use the Flexible Pool or carry it to next month instead.");
            }
        }

        var (fromKind, fromCategoryId) = method switch
        {
            DeficitResolutionMethod.OwnSavings => ("CategorySavings", (Guid?)request.CategoryId),
            DeficitResolutionMethod.OtherCategorySavings => ("CategorySavings", request.SourceCategoryId),
            DeficitResolutionMethod.FlexiblePool => ("FlexiblePool", (Guid?)null),
            _ => throw new ConflictException("This method is not supported yet, it will be available once month close ships."),
        };

        // This is what actually moves the money: a normal reallocation, tagged as a
        // deficit cover, source has to really have the funds, same as any other.
        var reallocation = await _reallocationService.CreateAsync(
            userId,
            new CreateReallocationRequest(
                new AccountRefInput(fromKind, fromCategoryId),
                new AccountRefInput("Category", request.CategoryId),
                request.Amount,
                "DeficitCover",
                request.Note,
                null),
            cancellationToken);

        var resolution = new DeficitResolution
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BudgetMonthId = budgetMonth.Id,
            CategoryId = request.CategoryId,
            Amount = new Money(request.Amount),
            Method = method,
            SourceCategoryId = request.SourceCategoryId,
            ResolvedOn = today,
            ResolvedByUserId = userId,
            Note = request.Note,
            ReallocationId = reallocation.Id,
            CreatedAt = _clock.UtcNow,
        };

        _dbContext.DeficitResolutions.Add(resolution);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _auditLogger.LogAsync(userId, "User", "DeficitResolved", "DeficitResolution", resolution.Id.ToString(), null, null, cancellationToken);

        return ToDto(resolution);
    }

    public async Task<IReadOnlyList<DeficitHistoryItemDto>> GetHistoryAsync(
        Guid userId, Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DeficitResolutions.Where(d => d.UserId == userId);

        if (categoryId is not null)
        {
            query = query.Where(d => d.CategoryId == categoryId);
        }

        if (from is not null)
        {
            query = query.Where(d => d.ResolvedOn >= from);
        }

        if (to is not null)
        {
            query = query.Where(d => d.ResolvedOn <= to);
        }

        var resolutions = await query.OrderByDescending(d => d.ResolvedOn).ToListAsync(cancellationToken);

        return resolutions
            .Select(d => new DeficitHistoryItemDto(d.ResolvedOn.Year, d.ResolvedOn.Month, d.CategoryId, d.Amount.Amount, d.Method.ToString()))
            .ToList();
    }

    private async Task<List<SuggestedSourceDto>> SuggestedSourcesAsync(
        Guid userId, Guid categoryId, Dictionary<Guid, Category> categories, bool deficitCoverFromSavingsEnabled, CancellationToken cancellationToken)
    {
        var sources = new List<SuggestedSourceDto>();

        // Free never suggests a savings source, even one with real money sitting in
        // it, docs/00-plan.md section 5 only allows FlexiblePool or carrying the
        // deficit forward on Free, offering a source that would just get rejected
        // at resolve time would be a dishonest suggestion.
        if (deficitCoverFromSavingsEnabled)
        {
            var ownSavings = await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.CategorySavings(categoryId), cancellationToken);
            if (ownSavings > Money.Zero)
            {
                sources.Add(new SuggestedSourceDto("OwnSavings", categoryId, ownSavings.Amount));
            }

            foreach (var other in categories.Values.Where(c => c.Id != categoryId && !c.IsArchived))
            {
                var otherSavings = await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.CategorySavings(other.Id), cancellationToken);
                if (otherSavings > Money.Zero)
                {
                    sources.Add(new SuggestedSourceDto("OtherCategorySavings", other.Id, otherSavings.Amount));
                }
            }
        }

        var poolBalance = await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.FlexiblePool(), cancellationToken);
        if (poolBalance > Money.Zero)
        {
            sources.Add(new SuggestedSourceDto("FlexiblePool", null, poolBalance.Amount));
        }

        return sources;
    }

    private static DeficitResolutionDto ToDto(DeficitResolution d) => new(
        d.Id, d.CategoryId, d.Amount.Amount, d.Method.ToString(), d.SourceCategoryId, d.ResolvedOn, d.Note);
}
