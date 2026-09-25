using FluentValidation;
using Natoshare.Application.Common;

namespace Natoshare.Application.Budgeting;

// Both "Standard" and "FixedAccount" are the only real category kinds, this is shared
// by every validator below that takes a Kind string.
internal static class CategoryKindValidation
{
    public static bool IsKnownKind(string kind) => kind is "Standard" or "FixedAccount";
}

// The one rule every allocation list has to pass: real categories, no repeats, and
// percentages that add up to exactly 100.
internal static class AllocationListValidation
{
    public static bool SumsToOneHundred(List<CategoryAllocationInput> allocations) =>
        allocations.Count > 0 && allocations.Sum(a => a.Percentage) == 100m;

    public static bool HasNoDuplicateCategories(List<CategoryAllocationInput> allocations) =>
        allocations.Select(a => a.CategoryId).Distinct().Count() == allocations.Count;

    public static bool EveryPercentageIsPositive(List<CategoryAllocationInput> allocations) =>
        allocations.All(a => a.Percentage > 0m);
}

public class MonthInputValidator : AbstractValidator<MonthInput>
{
    public MonthInputValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Kind).Must(CategoryKindValidation.IsKnownKind)
            .WithMessage("Kind must be 'Standard' or 'FixedAccount'.");
        RuleFor(x => x.ExternalAccountLabel).MaximumLength(120);
    }
}

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name!).NotEmpty().MaximumLength(80).When(x => x.Name is not null);
        RuleFor(x => x.ExternalAccountLabel).MaximumLength(120);
    }
}

public class AddSubCategoryRequestValidator : AbstractValidator<AddSubCategoryRequest>
{
    public AddSubCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(60);
    }
}

public class CreateAllocationVersionRequestValidator : AbstractValidator<CreateAllocationVersionRequest>
{
    public CreateAllocationVersionRequestValidator()
    {
        RuleFor(x => x.FixedIncomeAmount).GreaterThan(0m);
        RuleFor(x => x.EffectiveFromMonth).SetValidator(new MonthInputValidator());

        RuleFor(x => x.Allocations)
            .Must(AllocationListValidation.EveryPercentageIsPositive)
            .WithMessage("Every category in the split needs a percentage above 0.")
            .Must(AllocationListValidation.HasNoDuplicateCategories)
            .WithMessage("The same category cannot appear twice in one split.")
            .Must(AllocationListValidation.SumsToOneHundred)
            .WithMessage("The percentages must add up to exactly 100.");
    }
}

public class ArchiveCategoryRequestValidator : AbstractValidator<ArchiveCategoryRequest>
{
    public ArchiveCategoryRequestValidator()
    {
        RuleFor(x => x.NewAllocations)
            .Must(AllocationListValidation.EveryPercentageIsPositive)
            .WithMessage("Every remaining category needs a percentage above 0.")
            .Must(AllocationListValidation.HasNoDuplicateCategories)
            .WithMessage("The same category cannot appear twice in one split.")
            .Must(AllocationListValidation.SumsToOneHundred)
            .WithMessage("The percentages must add up to exactly 100.");

        RuleFor(x => x.EffectiveFromMonth!).SetValidator(new MonthInputValidator()).When(x => x.EffectiveFromMonth is not null);
    }
}

public class AllocationPreviewRequestValidator : AbstractValidator<AllocationPreviewRequest>
{
    public AllocationPreviewRequestValidator()
    {
        RuleFor(x => x.FixedIncomeAmount).GreaterThan(0m);
        RuleFor(x => x.Allocations)
            .NotEmpty()
            .Must(AllocationListValidation.EveryPercentageIsPositive)
            .Must(AllocationListValidation.HasNoDuplicateCategories);
    }
}

public class OnboardingCategoryInputValidator : AbstractValidator<OnboardingCategoryInput>
{
    public OnboardingCategoryInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Kind).Must(CategoryKindValidation.IsKnownKind)
            .WithMessage("Kind must be 'Standard' or 'FixedAccount'.");
        RuleFor(x => x.Percentage).GreaterThan(0m);
    }
}

public class OnboardingCompleteRequestValidator : AbstractValidator<OnboardingCompleteRequest>
{
    public OnboardingCompleteRequestValidator(ICurrencyCatalog currencyCatalog)
    {
        RuleFor(x => x.Currency.Code)
            .NotEmpty()
            .Length(3)
            .Must(currencyCatalog.IsValidCode)
            .WithMessage("'{PropertyValue}' is not a real ISO 4217 currency code.");
        RuleFor(x => x.Currency.Symbol).NotEmpty().MaximumLength(8);

        RuleFor(x => x.TimeZoneId)
            .Must(LocaleValidation.IsKnownTimeZone)
            .WithMessage("'{PropertyValue}' is not a real timezone id.");

        RuleFor(x => x.Locale!)
            .Must(LocaleValidation.IsKnownLocale)
            .WithMessage("'{PropertyValue}' is not a real locale.")
            .When(x => x.Locale is not null);

        RuleFor(x => x.FixedIncomeAmount).GreaterThan(0m);
        RuleFor(x => x.EffectiveFromMonth).SetValidator(new MonthInputValidator());

        RuleForEach(x => x.Categories).SetValidator(new OnboardingCategoryInputValidator());

        RuleFor(x => x.Categories)
            .Must(categories => categories.Count > 0)
            .WithMessage("You need at least one category.")
            .Must(HaveUniqueNames)
            .WithMessage("Two categories cannot have the same name.")
            .Must(SumToOneHundred)
            .WithMessage("The percentages must add up to exactly 100.");
    }

    private static bool HaveUniqueNames(List<OnboardingCategoryInput> categories) =>
        categories.Select(c => c.Name.Trim().ToLowerInvariant()).Distinct().Count() == categories.Count;

    private static bool SumToOneHundred(List<OnboardingCategoryInput> categories) =>
        categories.Sum(c => c.Percentage) == 100m;
}
