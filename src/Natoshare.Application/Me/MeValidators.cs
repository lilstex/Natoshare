using FluentValidation;
using Natoshare.Application.Common;

namespace Natoshare.Application.Me;

public class UpdateMeRequestValidator : AbstractValidator<UpdateMeRequest>
{
    public UpdateMeRequestValidator()
    {
        RuleFor(x => x.DisplayName!)
            .NotEmpty().MaximumLength(80)
            .When(x => x.DisplayName is not null);

        RuleFor(x => x.TimeZoneId!)
            .Must(LocaleValidation.IsKnownTimeZone).WithMessage("'{PropertyValue}' is not a real timezone id.")
            .When(x => x.TimeZoneId is not null);

        RuleFor(x => x.Locale!)
            .NotEmpty()
            .Must(LocaleValidation.IsKnownLocale).WithMessage("'{PropertyValue}' is not a real locale.")
            .When(x => x.Locale is not null);
    }
}

public class UpdateCurrencyRequestValidator : AbstractValidator<UpdateCurrencyRequest>
{
    public UpdateCurrencyRequestValidator(ICurrencyCatalog currencyCatalog)
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(3)
            .Must(currencyCatalog.IsValidCode).WithMessage("'{PropertyValue}' is not a real ISO 4217 currency code.");

        RuleFor(x => x.Symbol).NotEmpty().MaximumLength(8);
    }
}

public class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.ConfirmText)
            .Equal("DELETE")
            .WithMessage("Type DELETE to confirm you want to delete your account.");
    }
}
