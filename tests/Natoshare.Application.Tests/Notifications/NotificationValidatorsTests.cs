using FluentAssertions;
using Natoshare.Application.Notifications;

namespace Natoshare.Application.Tests.Notifications;

public class UpdateAlertPreferenceInputValidatorTests
{
    private readonly UpdateAlertPreferenceInputValidator _validator = new();

    [Fact]
    public void A_kind_that_does_not_exist_in_the_vocabulary_at_all_is_rejected()
    {
        // As of Phase 7 every kind in the documented NotificationKind vocabulary is
        // finally adjustable, so this test now covers a made-up kind instead of a
        // real one from a later phase, there is no "later phase" kind left.
        var input = new UpdateAlertPreferenceInput("SomethingThatDoesNotExist", true, null, null);
        _validator.Validate(input).IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_phase_six_people_and_money_kinds_are_accepted()
    {
        _validator.Validate(new UpdateAlertPreferenceInput("DebtDueSoon", true, null, 3)).IsValid.Should().BeTrue();
        _validator.Validate(new UpdateAlertPreferenceInput("DebtOverdue", true, null, null)).IsValid.Should().BeTrue();
        _validator.Validate(new UpdateAlertPreferenceInput("LoanReturnDueSoon", true, null, 3)).IsValid.Should().BeTrue();
        _validator.Validate(new UpdateAlertPreferenceInput("LoanOverdue", true, null, null)).IsValid.Should().BeTrue();
        _validator.Validate(new UpdateAlertPreferenceInput("PromiseReminder", true, null, null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void The_phase_seven_recurring_item_kind_is_now_accepted()
    {
        var input = new UpdateAlertPreferenceInput("RecurringItemDue", true, null, null);
        _validator.Validate(input).IsValid.Should().BeTrue();
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
