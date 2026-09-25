using FluentAssertions;
using Natoshare.Application.Subscriptions;

namespace Natoshare.Application.Tests.Subscriptions;

public class UpgradeSubscriptionRequestValidatorTests
{
    private readonly UpgradeSubscriptionRequestValidator _validator = new();

    [Fact]
    public void A_monthly_pro_upgrade_is_valid()
    {
        _validator.Validate(new UpgradeSubscriptionRequest("Pro", "monthly")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_annual_pro_upgrade_is_valid()
    {
        _validator.Validate(new UpgradeSubscriptionRequest("Pro", "annual")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Only_pro_can_be_requested()
    {
        _validator.Validate(new UpgradeSubscriptionRequest("Free", "monthly")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_unknown_billing_cycle_is_rejected()
    {
        _validator.Validate(new UpgradeSubscriptionRequest("Pro", "weekly")).IsValid.Should().BeFalse();
    }
}
