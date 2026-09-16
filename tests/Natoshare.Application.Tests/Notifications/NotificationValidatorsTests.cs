using FluentAssertions;
using Natoshare.Application.Notifications;

namespace Natoshare.Application.Tests.Notifications;

public class UpdateAlertPreferenceInputValidatorTests
{
    private readonly UpdateAlertPreferenceInputValidator _validator = new();

    [Fact]
    public void A_kind_from_a_later_phase_is_rejected()
    {
        // DebtDueSoon needs the DebtIn entity, which is Phase 6 work, so it cannot
        // be adjusted yet even though the name already exists in the vocabulary.
        var input = new UpdateAlertPreferenceInput("DebtDueSoon", true, null, null);
        _validator.Validate(input).IsValid.Should().BeFalse();
    }

    [Fact]
    public void One_of_the_currently_evaluated_kinds_is_accepted()
    {
        var input = new UpdateAlertPreferenceInput("OverPaceCategory", true, 120m, null);
        _validator.Validate(input).IsValid.Should().BeTrue();
    }

    [Fact]
    public void The_phase_five_month_close_reminder_kind_is_now_accepted()
    {
        var input = new UpdateAlertPreferenceInput("MonthCloseReminder", true, null, 5);
        _validator.Validate(input).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_threshold_outside_the_sane_range_is_rejected()
    {
        var input = new UpdateAlertPreferenceInput("SafeToSpendLow", true, 0m, null);
        _validator.Validate(input).IsValid.Should().BeFalse();
    }
}
