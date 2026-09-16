using FluentValidation;

namespace Natoshare.Application.Notifications;

internal static class NotificationKindValidation
{
    // Only alert kinds a real use-case actually evaluates can be turned on or off
    // here, the rest belong to features that do not exist yet.
    public static bool IsAdjustableNow(string kind) =>
        kind is "OverPaceCategory" or "OverspendCategory" or "CategoryInDeficit" or "SafeToSpendLow"
            or "MonthCloseReminder" or "FixedAccountUnconfirmed" or "CarriedDeficitApplied" or "MonthEndSummary";
}

public class UpdateAlertPreferenceInputValidator : AbstractValidator<UpdateAlertPreferenceInput>
{
    public UpdateAlertPreferenceInputValidator()
    {
        RuleFor(x => x.Kind).Must(NotificationKindValidation.IsAdjustableNow)
            .WithMessage("'{PropertyValue}' is not a preference you can change yet.");
        RuleFor(x => x.ThresholdPercent!.Value).InclusiveBetween(1m, 500m).When(x => x.ThresholdPercent is not null);
        RuleFor(x => x.LeadDays!.Value).InclusiveBetween(0, 60).When(x => x.LeadDays is not null);
    }
}

// PATCH /notifications/preferences takes a plain JSON array in the body, this
// validates every item in it the same way.
public class UpdateAlertPreferenceListValidator : AbstractValidator<List<UpdateAlertPreferenceInput>>
{
    public UpdateAlertPreferenceListValidator()
    {
        RuleForEach(x => x).SetValidator(new UpdateAlertPreferenceInputValidator());
    }
}
