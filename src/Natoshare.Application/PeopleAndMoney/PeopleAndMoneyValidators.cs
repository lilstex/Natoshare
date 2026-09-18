using FluentValidation;
using Natoshare.Application.Ledger;

namespace Natoshare.Application.PeopleAndMoney;

public class CreateLoanOutRequestValidator : AbstractValidator<CreateLoanOutRequest>
{
    public CreateLoanOutRequestValidator()
    {
        RuleFor(x => x.BorrowerName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.ExpectedReturnOn!.Value).GreaterThanOrEqualTo(x => x.LentOn)
            .WithMessage("The expected return date cannot be before the date it was lent.")
            .When(x => x.ExpectedReturnOn is not null);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);
        RuleFor(x => x.LinkedSource!).SetValidator(new AccountRefInputValidator()).When(x => x.LinkedSource is not null);
    }
}

public class CreateLoanRepaymentRequestValidator : AbstractValidator<CreateLoanRepaymentRequest>
{
    public CreateLoanRepaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);
        RuleFor(x => x.LinkedDestination!).SetValidator(new AccountRefInputValidator()).When(x => x.LinkedDestination is not null);
    }
}

public class UpdateLoanOutRequestValidator : AbstractValidator<UpdateLoanOutRequest>
{
    public UpdateLoanOutRequestValidator()
    {
        RuleFor(x => x.BorrowerName!).NotEmpty().MaximumLength(100).When(x => x.BorrowerName is not null);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);

        // Repaid, PartiallyRepaid and Outstanding are all worked out automatically
        // from repayments, WrittenOff is the only state a user ever sets by hand.
        RuleFor(x => x.Status!).Must(s => s == "WrittenOff")
            .WithMessage("Status can only be set to 'WrittenOff', the rest are worked out automatically from repayments.")
            .When(x => x.Status is not null);
    }
}

public class CreateDebtInRequestValidator : AbstractValidator<CreateDebtInRequest>
{
    public CreateDebtInRequestValidator()
    {
        RuleFor(x => x.LenderName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.DueOn!.Value).GreaterThanOrEqualTo(x => x.BorrowedOn)
            .WithMessage("The due date cannot be before the date it was borrowed.")
            .When(x => x.DueOn is not null);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);
    }
}

public class CreateDebtRepaymentRequestValidator : AbstractValidator<CreateDebtRepaymentRequest>
{
    public CreateDebtRepaymentRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);
        RuleFor(x => x.LinkedSource!).SetValidator(new AccountRefInputValidator()).When(x => x.LinkedSource is not null);
    }
}

public class UpdateDebtInRequestValidator : AbstractValidator<UpdateDebtInRequest>
{
    public UpdateDebtInRequestValidator()
    {
        RuleFor(x => x.LenderName!).NotEmpty().MaximumLength(100).When(x => x.LenderName is not null);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);

        // A debt has no manual terminal state like a loan's WrittenOff, its status is
        // always worked out automatically from repayments.
        RuleFor(x => x.Status!)
            .Must(_ => false)
            .WithMessage("Debt status is worked out automatically from repayments and cannot be set directly.")
            .When(x => x.Status is not null);
    }
}

public class CreatePromiseRequestValidator : AbstractValidator<CreatePromiseRequest>
{
    public CreatePromiseRequestValidator()
    {
        RuleFor(x => x.PersonName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);
    }
}

public class CreatePromiseRedemptionRequestValidator : AbstractValidator<CreatePromiseRedemptionRequest>
{
    public CreatePromiseRedemptionRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);
        RuleFor(x => x.SourceAccount).SetValidator(new AccountRefInputValidator());
    }
}

public class UpdatePromiseRequestValidator : AbstractValidator<UpdatePromiseRequest>
{
    public UpdatePromiseRequestValidator()
    {
        RuleFor(x => x.PersonName!).NotEmpty().MaximumLength(100).When(x => x.PersonName is not null);
        RuleFor(x => x.Amount!.Value).GreaterThan(0m).When(x => x.Amount is not null);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);

        // Open, PartiallyRedeemed and Redeemed are all worked out automatically from
        // redemptions, Cancelled is the only state a user ever sets by hand.
        RuleFor(x => x.Status!).Must(s => s == "Cancelled")
            .WithMessage("Status can only be set to 'Cancelled', the rest are worked out automatically from redemptions.")
            .When(x => x.Status is not null);
    }
}

public class CreateInvestmentLogRequestValidator : AbstractValidator<CreateInvestmentLogRequest>
{
    public CreateInvestmentLogRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0m);
        RuleFor(x => x.Platform).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Note!).MaximumLength(200).When(x => x.Note is not null);
    }
}
