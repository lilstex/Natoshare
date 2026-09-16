using FluentAssertions;
using Natoshare.Application.Notifications;

namespace Natoshare.Application.Tests.Notifications;

public class UpdateAlertPreferenceInputValidatorTests
{
    private readonly UpdateAlertPreferenceInputValidator _validator = new();

    [Fact]
    public void A_kind_from_a_later_phase_is_rejected()
    {
        var input = new UpdateAlertPreferenceInput("MonthCloseReminder", true, null, null);
        _validator.Validate(input).IsValid.Should().BeFalse();
    }

    [Fact]
    public void One_of_the_four_phase_four_kinds_is_accepted()
    {
        var input = new UpdateAlertPreferenceInput("OverPaceCategory", true, 120m, null);
        _validator.Validate(input).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_threshold_outside_the_sane_range_is_rejected()
    {
        var input = new UpdateAlertPreferenceInput("SafeToSpendLow", true, 0m, null);
        _validator.Validate(input).IsValid.Should().BeFalse();
    }
}
