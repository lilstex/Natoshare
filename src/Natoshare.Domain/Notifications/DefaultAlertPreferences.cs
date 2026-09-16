namespace Natoshare.Domain.Notifications;

public record DefaultAlertPreferenceDefinition(NotificationKind Kind, decimal? ThresholdPercent, int? LeadDays);

// The alert settings every new account starts with, so alerts already work on day
// one without the user having to visit a settings page first. Only kinds a real
// use-case already produces are seeded, the rest get seeded once their own phase
// ships. This only affects accounts created from here on, existing accounts keep
// whatever list they were seeded with when they signed up.
public static class DefaultAlertPreferences
{
    public static readonly IReadOnlyList<DefaultAlertPreferenceDefinition> Items =
    [
        // 110 means "warn once projected spend passes 10% over what is funded".
        new(NotificationKind.OverPaceCategory, ThresholdPercent: 110m, LeadDays: null),
        new(NotificationKind.OverspendCategory, ThresholdPercent: null, LeadDays: null),
        new(NotificationKind.CategoryInDeficit, ThresholdPercent: null, LeadDays: null),
        // 20 means "warn once today's safe-to-spend rate drops below 20% of the flat
        // daily rate the category started the month with".
        new(NotificationKind.SafeToSpendLow, ThresholdPercent: 20m, LeadDays: null),
        // 3 means "remind 3 days after the month ends if it is still sitting open".
        new(NotificationKind.MonthCloseReminder, ThresholdPercent: null, LeadDays: 3),
        new(NotificationKind.FixedAccountUnconfirmed, ThresholdPercent: null, LeadDays: null),
        new(NotificationKind.CarriedDeficitApplied, ThresholdPercent: null, LeadDays: null),
        new(NotificationKind.MonthEndSummary, ThresholdPercent: null, LeadDays: null),
    ];
}
