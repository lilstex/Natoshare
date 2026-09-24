using FluentAssertions;
using Natoshare.Application.Admin;

namespace Natoshare.Application.Tests.Admin;

public class SuspendUserRequestValidatorTests
{
    private readonly SuspendUserRequestValidator _validator = new();

    [Fact]
    public void A_reason_is_required()
    {
        _validator.Validate(new SuspendUserRequest("")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_short_reason_is_valid()
    {
        _validator.Validate(new SuspendUserRequest("Chargeback fraud reported")).IsValid.Should().BeTrue();
    }
}

public class AdjustPlanRequestValidatorTests
{
    private readonly AdjustPlanRequestValidator _validator = new();

    [Fact]
    public void A_plan_of_pro_is_valid()
    {
        _validator.Validate(new AdjustPlanRequest("Pro", null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_unknown_plan_name_is_rejected()
    {
        _validator.Validate(new AdjustPlanRequest("Premium", null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Neither_a_plan_nor_a_trial_end_date_is_rejected()
    {
        _validator.Validate(new AdjustPlanRequest(null, null)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_trial_end_date_alone_is_valid()
    {
        _validator.Validate(new AdjustPlanRequest(null, DateTimeOffset.UtcNow.AddDays(30))).IsValid.Should().BeTrue();
    }
}

public class HardDeleteUserRequestValidatorTests
{
    private readonly HardDeleteUserRequestValidator _validator = new();

    [Fact]
    public void An_empty_confirm_text_is_rejected()
    {
        _validator.Validate(new HardDeleteUserRequest("")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Any_non_empty_confirm_text_passes_this_validator()
    {
        // The actual "does this match the account's email" check happens in
        // AdminUserService, not here, this validator only checks the field was sent.
        _validator.Validate(new HardDeleteUserRequest("someone@example.com")).IsValid.Should().BeTrue();
    }
}

public class PatchPlanConfigRequestValidatorTests
{
    private readonly PatchPlanConfigRequestValidator _validator = new();

    [Fact]
    public void Unlimited_categories_and_history_are_valid()
    {
        _validator.Validate(new PatchPlanConfigRequest(null, null, true, true, true, true)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_zero_category_limit_is_rejected()
    {
        _validator.Validate(new PatchPlanConfigRequest(0, 60, false, false, false, false)).IsValid.Should().BeFalse();
    }
}
