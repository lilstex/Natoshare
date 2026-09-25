using FluentValidation;
using Natoshare.Application.Ledger;

namespace Natoshare.Application.Months;

internal static class DeficitResolutionMethodValidation
{
    // Unlike the mid-month /deficits/resolve endpoint, closing a month can actually
    // use NextMonthAllocation, this is the one moment it means something: the deficit
    // gets carried into next month's numbers instead of covered from somewhere else.
    public static bool IsKnownMethod(string method) =>
        method is "OwnSavings" or "OtherCategorySavings" or "FlexiblePool" or "NextMonthAllocation";
}

public class ConfirmFixedAccountRequestValidator : AbstractValidator<ConfirmFixedAccountRequest>
{
    public ConfirmFixedAccountRequestValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}

public class CloseDeficitResolutionInputValidator : AbstractValidator<CloseDeficitResolutionInput>
{
    public CloseDeficitResolutionInputValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Method).Must(DeficitResolutionMethodValidation.IsKnownMethod)
            .WithMessage("Method must be OwnSavings, OtherCategorySavings, FlexiblePool or NextMonthAllocation.");
        RuleFor(x => x.SourceCategoryId).NotNull()
            .WithMessage("sourceCategoryId is required when covering from another category's savings.")
            .When(x => x.Method == "OtherCategorySavings");
    }
}

public class RebalanceInputValidator : AbstractValidator<RebalanceInput>
{
    public RebalanceInputValidator()
    {
        RuleFor(x => x.FromAccount).SetValidator(new AccountRefInputValidator());
        RuleFor(x => x.ToAccount).SetValidator(new AccountRefInputValidator());
        RuleFor(x => x.Amount).GreaterThan(0m);
    }
}

public class CloseMonthRequestValidator : AbstractValidator<CloseMonthRequest>
{
    public CloseMonthRequestValidator()
    {
        RuleForEach(x => x.DeficitResolutions).SetValidator(new CloseDeficitResolutionInputValidator());
        RuleFor(x => x.DeficitResolutions)
            .Must(list => list.Select(d => d.CategoryId).Distinct().Count() == list.Count)
            .WithMessage("Only one resolution is needed per category.");

        RuleForEach(x => x.FixedAccountConfirmations!).SetValidator(new ConfirmFixedAccountRequestValidator())
            .When(x => x.FixedAccountConfirmations is not null);

        RuleForEach(x => x.Rebalances!).SetValidator(new RebalanceInputValidator())
            .When(x => x.Rebalances is not null);

        RuleFor(x => x.PromiseRedemptions)
            .Must(list => list is null or { Count: 0 })
            .WithMessage("Promises are not available yet.");
    }
}

public class ReopenMonthRequestValidator : AbstractValidator<ReopenMonthRequest>
{
    public ReopenMonthRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(300);
    }
}
