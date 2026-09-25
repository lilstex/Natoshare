using FluentValidation;

namespace Natoshare.Application.Ledger;

internal static class AccountRefValidation
{
    // A manual reallocation can only move money between a category, its savings, or
    // the Flexible Pool. "External" (money already moved out to a bank or platform)
    // is only ever written by the month-close flow (Phase 5), never picked by a user.
    public static bool IsAllowedKind(string kind) => kind is "Category" or "CategorySavings" or "FlexiblePool";

    public static bool HasCategoryIdWhenNeeded(AccountRefInput account) =>
        account.Kind == "FlexiblePool" || account.CategoryId is not null;
}

public class AccountRefInputValidator : AbstractValidator<AccountRefInput>
{
    public AccountRefInputValidator()
    {
        RuleFor(x => x.Kind).Must(AccountRefValidation.IsAllowedKind)
            .WithMessage("Kind must be 'Category', 'CategorySavings' or 'FlexiblePool'.");
        RuleFor(x => x).Must(AccountRefValidation.HasCategoryIdWhenNeeded)
            .WithMessage("categoryId is required for this account kind.");
    }
}

public class LogIncomeRequestValidator : AbstractValidator<LogIncomeRequest>
{
    public LogIncomeRequestValidator()
    {
        RuleFor(x => x.Type).Must(t => t is "Allocatable" or "Flexible")
            .WithMessage("Type must be 'Allocatable' or 'Flexible'.");
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
    }
}

public class UpdateIncomeRequestValidator : AbstractValidator<UpdateIncomeRequest>
{
    public UpdateIncomeRequestValidator()
    {
        RuleFor(x => x.Amount!.Value).GreaterThan(0m).When(x => x.Amount is not null);
        RuleFor(x => x.Description!).NotEmpty().MaximumLength(200).When(x => x.Description is not null);
    }
}

public class LogExpenseRequestValidator : AbstractValidator<LogExpenseRequest>
{
    public LogExpenseRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Source).Must(s => s is "Category" or "FlexiblePool")
            .WithMessage("Source must be 'Category' or 'FlexiblePool'.");
        RuleFor(x => x.CategoryId).NotNull()
            .WithMessage("categoryId is required when the source is a category.")
            .When(x => x.Source == "Category");
        RuleFor(x => x.SubCategory!).MaximumLength(60).When(x => x.SubCategory is not null);
        RuleForEach(x => x.Tags!).NotEmpty().MaximumLength(40).When(x => x.Tags is not null);
    }
}

public class UpdateExpenseRequestValidator : AbstractValidator<UpdateExpenseRequest>
{
    public UpdateExpenseRequestValidator()
    {
        RuleFor(x => x.Amount!.Value).GreaterThan(0m).When(x => x.Amount is not null);
        RuleFor(x => x.Description!).NotEmpty().MaximumLength(200).When(x => x.Description is not null);
        RuleFor(x => x.SubCategory!).MaximumLength(60).When(x => x.SubCategory is not null);
        RuleForEach(x => x.Tags!).NotEmpty().MaximumLength(40).When(x => x.Tags is not null);
    }
}

internal static class DeficitResolutionMethodValidation
{
    // NextMonthAllocation is a month-close decision (Phase 5), so it is not accepted
    // here yet, only the three methods that move real money in right now are.
    public static bool IsSupportedNow(string method) => method is "OwnSavings" or "OtherCategorySavings" or "FlexiblePool";
}

public class ResolveDeficitRequestValidator : AbstractValidator<ResolveDeficitRequest>
{
    public ResolveDeficitRequestValidator()
    {
        RuleFor(x => x.Year).InclusiveBetween(2020, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Method).Must(DeficitResolutionMethodValidation.IsSupportedNow)
            .WithMessage("Method must be 'OwnSavings', 'OtherCategorySavings' or 'FlexiblePool'. Carrying a deficit to next month is chosen when the month closes.");
        RuleFor(x => x.SourceCategoryId).NotNull()
            .WithMessage("sourceCategoryId is required when covering from another category's savings.")
            .When(x => x.Method == "OtherCategorySavings");
    }
}

public class CreateReallocationRequestValidator : AbstractValidator<CreateReallocationRequest>
{
    public CreateReallocationRequestValidator()
    {
        RuleFor(x => x.FromAccount).SetValidator(new AccountRefInputValidator());
        RuleFor(x => x.ToAccount).SetValidator(new AccountRefInputValidator());
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Reason).Must(r => r is "DeficitCover" or "MonthCloseRebalance" or "FundPromise" or "FundLoan" or "Manual")
            .WithMessage("Reason must be one of DeficitCover, MonthCloseRebalance, FundPromise, FundLoan, Manual.");
        RuleFor(x => x).Must(x => x.FromAccount.Kind != x.ToAccount.Kind || x.FromAccount.CategoryId != x.ToAccount.CategoryId)
            .WithMessage("The source and destination account cannot be the same account.");
    }
}
