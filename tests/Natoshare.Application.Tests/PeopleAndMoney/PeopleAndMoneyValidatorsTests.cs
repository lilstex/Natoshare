using FluentAssertions;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Application.Tests.PeopleAndMoney;

public class CreateLoanOutRequestValidatorTests
{
    private readonly CreateLoanOutRequestValidator _validator = new();

    [Fact]
    public void An_expected_return_date_before_the_lending_date_is_rejected()
    {
        var request = new CreateLoanOutRequest("Tunde", 20000m, new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 1), null, null);
        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_linked_source_can_only_be_category_categorySavings_or_flexiblePool()
    {
        var request = new CreateLoanOutRequest(
            "Tunde", 20000m, new DateOnly(2026, 9, 17), null, null, new AccountRefInput("External", Guid.NewGuid()));
        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void No_linked_source_at_all_is_perfectly_valid()
    {
        var request = new CreateLoanOutRequest("Tunde", 20000m, new DateOnly(2026, 9, 17), null, null, null);
        _validator.Validate(request).IsValid.Should().BeTrue();
    }
}

public class UpdateLoanOutRequestValidatorTests
{
    private readonly UpdateLoanOutRequestValidator _validator = new();

    [Fact]
    public void WrittenOff_is_the_only_status_a_user_can_set_by_hand()
    {
        _validator.Validate(new UpdateLoanOutRequest(null, null, null, "WrittenOff")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Repaid_cannot_be_set_by_hand_it_is_worked_out_from_repayments()
    {
        _validator.Validate(new UpdateLoanOutRequest(null, null, null, "Repaid")).IsValid.Should().BeFalse();
    }
}

public class UpdateDebtInRequestValidatorTests
{
    private readonly UpdateDebtInRequestValidator _validator = new();

    [Fact]
    public void A_debt_has_no_manual_status_at_all()
    {
        _validator.Validate(new UpdateDebtInRequest(null, null, null, "Repaid")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Leaving_status_out_entirely_is_fine()
    {
        _validator.Validate(new UpdateDebtInRequest("Ada", null, null, null)).IsValid.Should().BeTrue();
    }
}

public class UpdatePromiseRequestValidatorTests
{
    private readonly UpdatePromiseRequestValidator _validator = new();

    [Fact]
    public void Cancelled_is_the_only_status_a_user_can_set_by_hand()
    {
        _validator.Validate(new UpdatePromiseRequest(null, null, null, "Cancelled")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Redeemed_cannot_be_set_by_hand_it_is_worked_out_from_redemptions()
    {
        _validator.Validate(new UpdatePromiseRequest(null, null, null, "Redeemed")).IsValid.Should().BeFalse();
    }
}

public class CreatePromiseRedemptionRequestValidatorTests
{
    private readonly CreatePromiseRedemptionRequestValidator _validator = new();

    [Fact]
    public void A_source_account_is_always_required_unlike_a_loan_or_debt()
    {
        var request = new CreatePromiseRedemptionRequest(4000m, new DateOnly(2026, 9, 18), new AccountRefInput("FlexiblePool", null), null);
        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_external_source_is_rejected_same_restricted_set_as_reallocations()
    {
        var request = new CreatePromiseRedemptionRequest(
            4000m, new DateOnly(2026, 9, 18), new AccountRefInput("External", Guid.NewGuid()), null);
        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
