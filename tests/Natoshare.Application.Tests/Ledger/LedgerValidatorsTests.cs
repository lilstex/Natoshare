using FluentAssertions;
using Natoshare.Application.Ledger;

namespace Natoshare.Application.Tests.Ledger;

public class LogExpenseRequestValidatorTests
{
    private readonly LogExpenseRequestValidator _validator = new();

    [Fact]
    public void A_category_expense_needs_a_categoryId()
    {
        var request = new LogExpenseRequest(1000m, "Lunch", "Category", null, null, null, null);
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "CategoryId");
    }

    [Fact]
    public void A_flexible_pool_expense_does_not_need_a_categoryId()
    {
        var request = new LogExpenseRequest(1000m, "Gift spend", "FlexiblePool", null, null, null, null);
        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_negative_or_zero_amount_is_rejected()
    {
        var request = new LogExpenseRequest(0m, "Nothing", "FlexiblePool", null, null, null, null);
        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}

public class ResolveDeficitRequestValidatorTests
{
    private readonly ResolveDeficitRequestValidator _validator = new();

    [Fact]
    public void NextMonthAllocation_is_not_accepted_until_month_close_ships()
    {
        var request = new ResolveDeficitRequest(Guid.NewGuid(), 2026, 9, 5000m, "NextMonthAllocation", null, null);
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Method");
    }

    [Fact]
    public void OtherCategorySavings_requires_a_source_category()
    {
        var request = new ResolveDeficitRequest(Guid.NewGuid(), 2026, 9, 5000m, "OtherCategorySavings", null, null);
        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SourceCategoryId");
    }

    [Fact]
    public void FlexiblePool_is_accepted_without_a_source_category()
    {
        var request = new ResolveDeficitRequest(Guid.NewGuid(), 2026, 9, 5000m, "FlexiblePool", null, null);
        _validator.Validate(request).IsValid.Should().BeTrue();
    }
}

public class CreateReallocationRequestValidatorTests
{
    private readonly CreateReallocationRequestValidator _validator = new();

    [Fact]
    public void Moving_money_to_the_same_account_is_rejected()
    {
        var categoryId = Guid.NewGuid();
        var request = new CreateReallocationRequest(
            new AccountRefInput("Category", categoryId),
            new AccountRefInput("Category", categoryId),
            1000m, "Manual", null, null);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void External_is_not_a_kind_a_user_can_pick_for_a_manual_move()
    {
        var request = new CreateReallocationRequest(
            new AccountRefInput("External", Guid.NewGuid()),
            new AccountRefInput("FlexiblePool", null),
            1000m, "Manual", null, null);

        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_normal_category_to_pool_move_is_valid()
    {
        var request = new CreateReallocationRequest(
            new AccountRefInput("Category", Guid.NewGuid()),
            new AccountRefInput("FlexiblePool", null),
            1000m, "Manual", null, null);

        _validator.Validate(request).IsValid.Should().BeTrue();
    }
}
