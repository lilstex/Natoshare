namespace Natoshare.Application.Budgeting;

// A calendar month, sent and received as plain year/month numbers so the API never
// has to argue with a client about time zones for something that is really just
// "September 2026".
public record MonthInput(int Year, int Month);

public record CategoryDto(
    Guid Id,
    string Name,
    string Kind,
    string? ExternalAccountLabel,
    IReadOnlyList<string> SubCategories,
    int SortOrder,
    bool IsArchived,
    decimal? CurrentPercentage);

public record CreateCategoryRequest(string Name, string Kind, string? ExternalAccountLabel, List<string>? SubCategories);

public record UpdateCategoryRequest(string? Name, int? SortOrder, string? ExternalAccountLabel);

public record AddSubCategoryRequest(string Name);

public record CategoryAllocationInput(Guid CategoryId, decimal Percentage);

public record ArchiveCategoryRequest(List<CategoryAllocationInput> NewAllocations, MonthInput? EffectiveFromMonth);

public record CreateAllocationVersionRequest(
    decimal FixedIncomeAmount,
    MonthInput EffectiveFromMonth,
    List<CategoryAllocationInput> Allocations,
    string? Note);

public record AllocationCategoryResult(Guid CategoryId, string Name, string Kind, decimal Percentage, decimal AllocatedAmount);

public record AllocationVersionResult(
    Guid Id,
    decimal FixedIncomeAmount,
    MonthInput EffectiveFromMonth,
    IReadOnlyList<AllocationCategoryResult> Categories,
    bool AffectedOpenMonthReSnapshotted);

public record CurrentAllocationResult(
    decimal FixedIncomeAmount,
    MonthInput EffectiveFromMonth,
    IReadOnlyList<AllocationCategoryResult> Categories);

public record AllocationPreviewRequest(decimal FixedIncomeAmount, List<CategoryAllocationInput> Allocations);

public record AllocationPreviewCategoryResult(Guid CategoryId, decimal Percentage, decimal AllocatedAmount);

public record AllocationPreviewResult(IReadOnlyList<AllocationPreviewCategoryResult> Categories, decimal Total);

public record BudgetTemplateItemDto(string Name, string Kind, decimal Percentage);

public record BudgetTemplateDto(Guid Id, string Name, string Description, IReadOnlyList<BudgetTemplateItemDto> Items);

public record OnboardingStateResult(string Step, bool LocaleSet, bool CurrencySet, bool IncomeSet, bool CategoriesSet, bool Done);

public record CurrencyInput(string Code, string Symbol);

public record OnboardingCategoryInput(
    string Name,
    string Kind,
    decimal Percentage,
    string? ExternalAccountLabel,
    List<string>? SubCategories);

public record OnboardingCompleteRequest(
    CurrencyInput Currency,
    string TimeZoneId,
    string? Locale,
    decimal FixedIncomeAmount,
    MonthInput EffectiveFromMonth,
    List<OnboardingCategoryInput> Categories);
