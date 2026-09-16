using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Budgeting;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Budgeting;

// The one-time wizard: currency, timezone, income and categories, all in one go.
public class OnboardingService : IOnboardingService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly UserManager<User> _userManager;
    private readonly AllocationService _allocationService;

    public OnboardingService(NatoshareDbContext dbContext, UserManager<User> userManager, AllocationService allocationService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _allocationService = allocationService;
    }

    public async Task<OnboardingStateResult> GetStateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("We could not find your account.");

        var hasAllocation = await _dbContext.AllocationConfigVersions.AnyAsync(v => v.UserId == userId, cancellationToken);
        var done = user.OnboardingCompletedAt is not null;

        return new OnboardingStateResult(
            Step: done ? "Complete" : "Income",
            LocaleSet: true,
            CurrencySet: true,
            IncomeSet: hasAllocation,
            CategoriesSet: hasAllocation,
            Done: done);
    }

    public async Task<IReadOnlyList<BudgetTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var templates = await _dbContext.BudgetTemplates
            .Include(t => t.Items)
            .OrderBy(t => t.SortOrder)
            .ToListAsync(cancellationToken);

        return templates.Select(t => new BudgetTemplateDto(
            t.Id,
            t.Name,
            t.Description,
            t.Items
                .OrderBy(i => i.SortOrder)
                .Select(i => new BudgetTemplateItemDto(i.Name, i.Kind.ToString(), i.Percentage))
                .ToList())).ToList();
    }

    public async Task<AllocationVersionResult> CompleteAsync(Guid userId, OnboardingCompleteRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new NotFoundException("We could not find your account.");

        if (user.OnboardingCompletedAt is not null)
        {
            throw new ConflictException("Onboarding is already complete, use the settings and category screens to make changes.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Onboarding only ever runs once, before the account has any real activity,
        // so we replace whatever categories the account was seeded with at signup
        // with exactly the set the wizard submits, instead of trying to reconcile
        // the two lists.
        var existingCategories = await _dbContext.Categories.Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        _dbContext.Categories.RemoveRange(existingCategories);

        var newCategories = request.Categories.Select((input, index) => new Category
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Name = input.Name.Trim(),
            Kind = Enum.Parse<CategoryKind>(input.Kind),
            ExternalAccountLabel = input.ExternalAccountLabel,
            SubCategories = input.SubCategories ?? [],
            SortOrder = index,
            CreatedAt = DateTimeOffset.UtcNow,
        }).ToList();

        _dbContext.Categories.AddRange(newCategories);

        user.CurrencyCode = request.Currency.Code.ToUpperInvariant();
        user.CurrencySymbol = request.Currency.Symbol;
        user.TimeZoneId = request.TimeZoneId;
        user.Locale = request.Locale ?? user.Locale;
        user.OnboardingCompletedAt = DateTimeOffset.UtcNow;
        await _userManager.UpdateAsync(user);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var effectiveFromMonth = new DateOnly(request.EffectiveFromMonth.Year, request.EffectiveFromMonth.Month, 1);
        var allocations = newCategories
            .Zip(request.Categories, (category, input) => new CategoryAllocationInput(category.Id, input.Percentage))
            .ToList();

        var version = await _allocationService.CreateVersionCoreAsync(
            userId,
            new CreateAllocationVersionRequest(request.FixedIncomeAmount, request.EffectiveFromMonth, allocations, "Onboarding"),
            effectiveFromMonth,
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var categoryLookup = newCategories.ToDictionary(c => c.Id);
        return AllocationService.ToVersionResult(version, categoryLookup);
    }
}
