using System.Globalization;
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
            .Must(BeAKnownTimeZone).WithMessage("'{PropertyValue}' is not a real timezone id.")
            .When(x => x.TimeZoneId is not null);

        RuleFor(x => x.Locale!)
            .NotEmpty()
            .Must(BeAKnownLocale).WithMessage("'{PropertyValue}' is not a real locale.")
            .When(x => x.Locale is not null);
    }

    // .NET already knows every IANA timezone on Linux, we just ask it instead of
    // keeping our own list.
    private static bool BeAKnownTimeZone(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    // CultureInfo.GetCultureInfo("anything-shaped-like-a-tag") almost never throws,
    // .NET treats most made up tags as a valid "custom" culture instead of rejecting
    // them. So instead we check the tag is in the real list of locales .NET ships,
    // which is what actually tells us it is a locale someone can use.
    private static readonly Lazy<HashSet<string>> KnownLocales = new(() =>
        new HashSet<string>(
            CultureInfo.GetCultures(CultureTypes.AllCultures).Select(c => c.Name),
            StringComparer.OrdinalIgnoreCase));

    private static bool BeAKnownLocale(string locale) => KnownLocales.Value.Contains(locale);
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
