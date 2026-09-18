using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Budgeting;

// Managing a user's categories: Rent, Feeding, and whatever else they set up.
public class CategoryService : ICategoryService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly AllocationService _allocationService;
    private readonly IEntitlementService _entitlementService;

    public CategoryService(NatoshareDbContext dbContext, AllocationService allocationService, IEntitlementService entitlementService)
    {
        _dbContext = dbContext;
        _allocationService = allocationService;
        _entitlementService = entitlementService;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(Guid userId, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Categories.Where(c => c.UserId == userId);
        if (!includeArchived)
        {
            query = query.Where(c => !c.IsArchived);
        }

        var categories = await query.OrderBy(c => c.SortOrder).ToListAsync(cancellationToken);
        var percentages = await CurrentPercentagesAsync(userId, cancellationToken);
        var lockedCategoryIds = await _entitlementService.GetLockedCategoryIdsAsync(userId, cancellationToken);

        return categories.Select(c => ToDto(c, PercentageOrNull(percentages, c.Id), lockedCategoryIds.Contains(c.Id))).ToList();
    }

    public async Task<CategoryDto> CreateCategoryAsync(Guid userId, CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var entitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
        if (entitlements.MaxCategories is not null)
        {
            var activeCount = await _dbContext.Categories.CountAsync(c => c.UserId == userId && !c.IsArchived, cancellationToken);
            if (activeCount >= entitlements.MaxCategories.Value)
            {
                throw new UpgradeRequiredException($"Free accounts can have up to {entitlements.MaxCategories.Value} categories, upgrade to Pro for unlimited categories.");
            }
        }

        await EnsureNameIsFreeAsync(userId, request.Name, existingCategoryId: null, cancellationToken);

        var maxSortOrder = await _dbContext.Categories
            .Where(c => c.UserId == userId)
            .Select(c => (int?)c.SortOrder)
            .MaxAsync(cancellationToken) ?? -1;

        var category = new Category
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = request.Name.Trim(),
            Kind = Enum.Parse<CategoryKind>(request.Kind),
            ExternalAccountLabel = request.ExternalAccountLabel,
            SubCategories = request.SubCategories ?? [],
            SortOrder = maxSortOrder + 1,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _dbContext.Categories.Add(category);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Just checked against the limit above in this same request, so this one
        // can never come back locked.
        return ToDto(category, currentPercentage: null, isLocked: false);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid userId, Guid categoryId, UpdateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await GetOwnedCategoryAsync(userId, categoryId, cancellationToken);

        if (request.Name is not null)
        {
            await EnsureNameIsFreeAsync(userId, request.Name, existingCategoryId: categoryId, cancellationToken);
            category.Name = request.Name.Trim();
        }

        if (request.SortOrder is not null)
        {
            category.SortOrder = request.SortOrder.Value;
        }

        if (request.ExternalAccountLabel is not null)
        {
            category.ExternalAccountLabel = request.ExternalAccountLabel;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var percentages = await CurrentPercentagesAsync(userId, cancellationToken);
        var isLocked = (await _entitlementService.GetLockedCategoryIdsAsync(userId, cancellationToken)).Contains(category.Id);
        return ToDto(category, PercentageOrNull(percentages, category.Id), isLocked);
    }

    public async Task<CategoryDto> AddSubCategoryAsync(Guid userId, Guid categoryId, AddSubCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await GetOwnedCategoryAsync(userId, categoryId, cancellationToken);
        var name = request.Name.Trim();

        if (!category.SubCategories.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase)))
        {
            category.SubCategories.Add(name);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var percentages = await CurrentPercentagesAsync(userId, cancellationToken);
        var isLocked = (await _entitlementService.GetLockedCategoryIdsAsync(userId, cancellationToken)).Contains(category.Id);
        return ToDto(category, PercentageOrNull(percentages, category.Id), isLocked);
    }

    public async Task<CategoryDto> RemoveSubCategoryAsync(Guid userId, Guid categoryId, string name, CancellationToken cancellationToken = default)
    {
        var category = await GetOwnedCategoryAsync(userId, categoryId, cancellationToken);
        category.SubCategories.RemoveAll(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
        await _dbContext.SaveChangesAsync(cancellationToken);

        var percentages = await CurrentPercentagesAsync(userId, cancellationToken);
        var isLocked = (await _entitlementService.GetLockedCategoryIdsAsync(userId, cancellationToken)).Contains(category.Id);
        return ToDto(category, PercentageOrNull(percentages, category.Id), isLocked);
    }

    public async Task<AllocationVersionResult> ArchiveCategoryAsync(
        Guid userId,
        Guid categoryId,
        ArchiveCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await GetOwnedCategoryAsync(userId, categoryId, cancellationToken);

        if (category.IsArchived)
        {
            throw new ConflictException("This category is already archived.");
        }

        if (request.NewAllocations.Any(a => a.CategoryId == categoryId))
        {
            throw new ConflictException("The category being archived cannot be part of its own replacement split.");
        }

        var currentAllocation = await _allocationService.GetCurrentAsync(userId, cancellationToken)
            ?? throw new ConflictException("Finish setting up your income and categories before archiving one.");

        var effectiveFromMonth = request.EffectiveFromMonth ?? await CurrentMonthInputAsync(userId, cancellationToken);

        // Archiving takes effect the same way any other split change does, through a
        // brand new version. The category itself is only marked archived once that
        // new version is safely saved.
        var newVersionRequest = new CreateAllocationVersionRequest(
            currentAllocation.FixedIncomeAmount,
            effectiveFromMonth,
            request.NewAllocations,
            $"Archived '{category.Name}'");

        var result = await _allocationService.CreateVersionAsync(userId, newVersionRequest, cancellationToken);

        category.IsArchived = true;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return result;
    }

    private async Task<Category> GetOwnedCategoryAsync(Guid userId, Guid categoryId, CancellationToken cancellationToken)
    {
        return await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == categoryId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that category.");
    }

    private async Task EnsureNameIsFreeAsync(Guid userId, string name, Guid? existingCategoryId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        var others = await _dbContext.Categories
            .Where(c => c.UserId == userId && c.Id != existingCategoryId)
            .Select(c => c.Name)
            .ToListAsync(cancellationToken);

        if (others.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException($"You already have a category called '{trimmed}'.");
        }
    }

    private async Task<Dictionary<Guid, decimal>> CurrentPercentagesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var current = await _allocationService.GetCurrentAsync(userId, cancellationToken);
        return current?.Categories.ToDictionary(c => c.CategoryId, c => c.Percentage) ?? [];
    }

    private async Task<MonthInput> CurrentMonthInputAsync(Guid userId, CancellationToken cancellationToken)
    {
        var month = await _allocationService.CurrentMonthForUserAsync(userId, cancellationToken);
        return new MonthInput(month.Year, month.Month);
    }

    private static decimal? PercentageOrNull(Dictionary<Guid, decimal> percentages, Guid categoryId) =>
        percentages.TryGetValue(categoryId, out var percentage) ? percentage : null;

    private static CategoryDto ToDto(Category category, decimal? currentPercentage, bool isLocked) => new(
        category.Id,
        category.Name,
        category.Kind.ToString(),
        category.ExternalAccountLabel,
        category.SubCategories,
        category.SortOrder,
        category.IsArchived,
        currentPercentage,
        isLocked);
}
