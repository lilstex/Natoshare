using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Budgeting;

// Everything to do with "how much do I earn, and how is it split", backed by
// Postgres.
public class AllocationService : IAllocationService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;

    public AllocationService(NatoshareDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<CurrentAllocationResult?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var versions = await LoadVersionsAsync(userId, cancellationToken);
        var thisMonth = await CurrentMonthForUserAsync(userId, cancellationToken);
        var active = AllocationResolver.ResolveActiveVersion(versions, thisMonth);

        if (active is null)
        {
            return null;
        }

        var categories = await CategoryLookupAsync(userId, cancellationToken);
        return new CurrentAllocationResult(
            active.FixedIncomeAmount.Amount,
            ToMonthInput(active.EffectiveFromMonth),
            BuildCategoryResults(active, categories));
    }

    public async Task<IReadOnlyList<AllocationVersionResult>> GetVersionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var versions = await LoadVersionsAsync(userId, cancellationToken, orderNewestFirst: true);
        var categories = await CategoryLookupAsync(userId, cancellationToken);

        return versions.Select(v => ToVersionResult(v, categories)).ToList();
    }

    public async Task<AllocationVersionResult> GetVersionAsync(Guid userId, Guid versionId, CancellationToken cancellationToken = default)
    {
        var version = await _dbContext.AllocationConfigVersions
            .Include(v => v.Allocations)
            .FirstOrDefaultAsync(v => v.Id == versionId && v.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that allocation version.");

        var categories = await CategoryLookupAsync(userId, cancellationToken);
        return ToVersionResult(version, categories);
    }

    public async Task<AllocationVersionResult> CreateVersionAsync(
        Guid userId,
        CreateAllocationVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        var effectiveFromMonth = new DateOnly(request.EffectiveFromMonth.Year, request.EffectiveFromMonth.Month, 1);
        var currentMonth = await CurrentMonthForUserAsync(userId, cancellationToken);

        // Phase 2 keeps this simple: redo the current month, or pick any future one.
        // Once Phase 3 adds real month-closing, this also rejects a month that has
        // already been closed for good.
        if (effectiveFromMonth < currentMonth)
        {
            throw new ConflictException("You can only set this from the current month onwards.");
        }

        // Once a month has real income or expenses logged against it (Phase 3), its
        // numbers are already snapshotted. Re-snapshotting an open month with new
        // percentages is real month-lifecycle work that belongs to Phase 5, not here,
        // so for now we just stop it from going stale instead of getting it wrong.
        var monthAlreadyOpened = await _dbContext.BudgetMonths.AnyAsync(
            m => m.UserId == userId && m.Year == effectiveFromMonth.Year && m.Month == effectiveFromMonth.Month, cancellationToken);
        if (monthAlreadyOpened)
        {
            throw new ConflictException("This month already has activity logged against it, changes can only apply from next month for now.");
        }

        var categoryIds = request.Allocations.Select(a => a.CategoryId).Distinct().ToList();
        var categories = await _dbContext.Categories
            .Where(c => c.UserId == userId && categoryIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        if (categories.Count != categoryIds.Count)
        {
            throw new ValidationFailedException(new Dictionary<string, string[]>
            {
                ["allocations"] = ["One or more categories were not found."],
            });
        }

        if (categories.Any(c => c.IsArchived))
        {
            throw new ConflictException("An archived category cannot be part of a new split.");
        }

        var version = await CreateVersionCoreAsync(userId, request, effectiveFromMonth, cancellationToken);

        var categoryLookup = categories.ToDictionary(c => c.Id);
        return ToVersionResult(version, categoryLookup);
    }

    public AllocationPreviewResult Preview(AllocationPreviewRequest request)
    {
        var money = new Money(request.FixedIncomeAmount);
        var percentages = request.Allocations.Select(a => a.Percentage).ToArray();
        var amounts = money.Allocate(percentages);

        var results = request.Allocations
            .Zip(amounts, (input, amount) => new AllocationPreviewCategoryResult(input.CategoryId, input.Percentage, amount.Amount))
            .ToList();

        return new AllocationPreviewResult(results, results.Sum(r => r.AllocatedAmount));
    }

    // The actual write: makes any existing version for the same month step aside,
    // then saves the new one. Shared by the plain "create a version" flow and by
    // anything else that needs to post a new split (onboarding, archiving a category).
    internal async Task<AllocationConfigVersion> CreateVersionCoreAsync(
        Guid userId,
        CreateAllocationVersionRequest request,
        DateOnly effectiveFromMonth,
        CancellationToken cancellationToken)
    {
        var existingSameMonth = await _dbContext.AllocationConfigVersions
            .Where(v => v.UserId == userId && v.EffectiveFromMonth == effectiveFromMonth && v.SupersededAt == null)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var existing in existingSameMonth)
        {
            existing.SupersededAt = now;
        }

        var version = new AllocationConfigVersion
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            FixedIncomeAmount = new Money(request.FixedIncomeAmount),
            EffectiveFromMonth = effectiveFromMonth,
            Note = request.Note,
            CreatedAt = now,
        };

        version.Allocations = request.Allocations.Select(a => new CategoryAllocation
        {
            Id = Guid.CreateVersion7(),
            AllocationConfigVersionId = version.Id,
            CategoryId = a.CategoryId,
            Percentage = a.Percentage,
        }).ToList();

        _dbContext.AllocationConfigVersions.Add(version);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return version;
    }

    internal async Task<DateOnly> CurrentMonthForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var timeZoneId = await _dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken) ?? "UTC";

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(_clock.UtcNow, timeZone);
        return new DateOnly(localNow.Year, localNow.Month, 1);
    }

    private async Task<List<AllocationConfigVersion>> LoadVersionsAsync(Guid userId, CancellationToken cancellationToken, bool orderNewestFirst = false)
    {
        var query = _dbContext.AllocationConfigVersions
            .Include(v => v.Allocations)
            .Where(v => v.UserId == userId);

        if (orderNewestFirst)
        {
            query = query.OrderByDescending(v => v.EffectiveFromMonth).ThenByDescending(v => v.CreatedAt);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, Category>> CategoryLookupAsync(Guid userId, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories.Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        return categories.ToDictionary(c => c.Id);
    }

    // Shared with OnboardingService, which posts the very first version for a new
    // account and needs to hand back the same shape.
    internal static AllocationVersionResult ToVersionResult(AllocationConfigVersion version, Dictionary<Guid, Category> categories)
    {
        return new AllocationVersionResult(
            version.Id,
            version.FixedIncomeAmount.Amount,
            ToMonthInput(version.EffectiveFromMonth),
            BuildCategoryResults(version, categories),
            AffectedOpenMonthReSnapshotted: false);
    }

    private static IReadOnlyList<AllocationCategoryResult> BuildCategoryResults(AllocationConfigVersion version, Dictionary<Guid, Category> categories)
    {
        var percentages = version.Allocations.Select(a => a.Percentage).ToArray();
        var amounts = version.FixedIncomeAmount.Allocate(percentages);

        var results = new List<AllocationCategoryResult>();
        for (var i = 0; i < version.Allocations.Count; i++)
        {
            var allocation = version.Allocations[i];
            categories.TryGetValue(allocation.CategoryId, out var category);

            results.Add(new AllocationCategoryResult(
                allocation.CategoryId,
                category?.Name ?? "(deleted category)",
                category?.Kind.ToString() ?? "Standard",
                allocation.Percentage,
                amounts[i].Amount));
        }

        return results;
    }

    private static MonthInput ToMonthInput(DateOnly month) => new(month.Year, month.Month);
}
