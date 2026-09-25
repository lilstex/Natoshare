using FluentAssertions;
using Natoshare.Application.Planning;

namespace Natoshare.Application.Tests.Planning;

public class CreateRecurringItemRequestValidatorTests
{
    private readonly CreateRecurringItemRequestValidator _validator = new();

    private static CreateRecurringItemRequest ExpenseRequest(int anchorDay = 15, string cadence = "Monthly") =>
        new("Expense", 5000m, "Netflix", null, null, cadence, anchorDay, "AutoPost");

    [Fact]
    public void A_plain_monthly_expense_is_valid()
    {
        _validator.Validate(ExpenseRequest()).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_income_item_needs_an_incomeType()
    {
        var request = new CreateRecurringItemRequest("Income", 500000m, "Salary", null, null, "Monthly", 25, "AutoPost");
        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_income_item_with_a_valid_incomeType_is_valid()
    {
        var request = new CreateRecurringItemRequest("Income", 500000m, "Salary", null, "Allocatable", "Monthly", 25, "AutoPost");
        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_income_item_cannot_carry_a_categoryId()
    {
        var request = new CreateRecurringItemRequest("Income", 500000m, "Salary", Guid.NewGuid(), "Allocatable", "Monthly", 25, "AutoPost");
        _validator.Validate(request).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Monthly_accepts_end_of_month_as_anchor_day_zero()
    {
        _validator.Validate(ExpenseRequest(anchorDay: 0)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Monthly_rejects_an_anchor_day_past_28()
    {
        _validator.Validate(ExpenseRequest(anchorDay: 29)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Weekly_accepts_an_anchor_day_up_to_six()
    {
        _validator.Validate(ExpenseRequest(anchorDay: 6, cadence: "Weekly")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Weekly_rejects_an_anchor_day_of_seven()
    {
        _validator.Validate(ExpenseRequest(anchorDay: 7, cadence: "Weekly")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_unknown_cadence_is_rejected()
    {
        _validator.Validate(ExpenseRequest(cadence: "Daily")).IsValid.Should().BeFalse();
    }
}

public class UpdateRecurringItemRequestValidatorTests
{
    private readonly UpdateRecurringItemRequestValidator _validator = new();

    [Fact]
    public void An_empty_update_is_valid()
    {
        var request = new UpdateRecurringItemRequest(null, null, null, null, null, null, null, null);
        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Changing_just_isActive_needs_nothing_else()
    {
        var request = new UpdateRecurringItemRequest(null, null, null, null, null, null, null, false);
        _validator.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_anchor_day_out_of_range_for_the_new_cadence_is_rejected()
    {
        var request = new UpdateRecurringItemRequest(null, null, null, null, "Weekly", 15, null, null);
        _validator.Validate(request).IsValid.Should().BeFalse();
    }
}
