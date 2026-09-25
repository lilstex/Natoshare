using FluentValidation;
using Natoshare.Domain.Planning;

namespace Natoshare.Application.Planning;

internal static class RecurringItemValidation
{
    public static bool IsValidAnchorDay(string cadence, int anchorDay) => cadence switch
    {
        "Monthly" => anchorDay is >= RecurringItem.EndOfMonthAnchorDay and <= 28,
        "Weekly" or "BiWeekly" => anchorDay is >= 0 and <= 6,
        _ => true, // an invalid cadence is already reported by its own rule
    };
}

public class CreateRecurringItemRequestValidator : AbstractValidator<CreateRecurringItemRequest>
{
    public CreateRecurringItemRequestValidator()
    {
        RuleFor(x => x.Kind).Must(k => k is "Expense" or "Income").WithMessage("Kind must be 'Expense' or 'Income'.");
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Cadence).Must(c => c is "Monthly" or "Weekly" or "BiWeekly")
            .WithMessage("Cadence must be 'Monthly', 'Weekly' or 'BiWeekly'.");
        RuleFor(x => x.Mode).Must(m => m is "Remind" or "AutoPost").WithMessage("Mode must be 'Remind' or 'AutoPost'.");
        RuleFor(x => x).Must(x => RecurringItemValidation.IsValidAnchorDay(x.Cadence, x.AnchorDay))
            .WithMessage("For Monthly, anchorDay must be 0 (end of month) to 28. For Weekly or BiWeekly, it must be 0 (Sunday) to 6 (Saturday).");
        RuleFor(x => x.IncomeType!).Must(t => t is "Allocatable" or "Flexible")
            .WithMessage("incomeType must be 'Allocatable' or 'Flexible' for an Income item.")
            .When(x => x.Kind == "Income");
        RuleFor(x => x.CategoryId).Null().WithMessage("An Income item has no category, only Expense does.").When(x => x.Kind == "Income");
    }
}

public class UpdateRecurringItemRequestValidator : AbstractValidator<UpdateRecurringItemRequest>
{
    public UpdateRecurringItemRequestValidator()
    {
        RuleFor(x => x.Amount!.Value).GreaterThan(0m).When(x => x.Amount is not null);
        RuleFor(x => x.Description!).NotEmpty().MaximumLength(200).When(x => x.Description is not null);
        RuleFor(x => x.Cadence!).Must(c => c is "Monthly" or "Weekly" or "BiWeekly")
            .WithMessage("Cadence must be 'Monthly', 'Weekly' or 'BiWeekly'.")
            .When(x => x.Cadence is not null);
        RuleFor(x => x.Mode!).Must(m => m is "Remind" or "AutoPost").WithMessage("Mode must be 'Remind' or 'AutoPost'.").When(x => x.Mode is not null);
        RuleFor(x => x).Must(x => RecurringItemValidation.IsValidAnchorDay(x.Cadence ?? "Monthly", x.AnchorDay!.Value))
            .WithMessage("For Monthly, anchorDay must be 0 (end of month) to 28. For Weekly or BiWeekly, it must be 0 (Sunday) to 6 (Saturday).")
            .When(x => x.AnchorDay is not null);
    }
}
