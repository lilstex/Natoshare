using FluentAssertions;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;
using Xunit;

namespace Natoshare.Application.Tests.Budgeting;

file sealed class FakeCurrencyCatalog : ICurrencyCatalog
{
    public bool IsValidCode(string code) => code is "NGN" or "USD" or "EUR";

    public IReadOnlyList<CurrencyOption> GetCommonCurrencies() => [];
}

public class CreateAllocationVersionRequestValidatorTests
{
    private readonly CreateAllocationVersionRequestValidator _validator = new();

    private static List<CategoryAllocationInput> DefaultSplit() =>
    [
        new(Guid.NewGuid(), 25m),
        new(Guid.NewGuid(), 25m),
        new(Guid.NewGuid(), 15m),
        new(Guid.NewGuid(), 10m),
        new(Guid.NewGuid(), 5m),
        new(Guid.NewGuid(), 20m),
    ];

    [Fact]
    public void Passes_when_percentages_add_up_to_exactly_100()
    {
        var request = new CreateAllocationVersionRequest(500_000m, new MonthInput(2026, 9), DefaultSplit(), null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Fails_when_percentages_do_not_add_up_to_100()
    {
        var allocations = new List<CategoryAllocationInput> { new(Guid.NewGuid(), 40m), new(Guid.NewGuid(), 40m) };
        var request = new CreateAllocationVersionRequest(500_000m, new MonthInput(2026, 9), allocations, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_same_category_appears_twice()
    {
        var categoryId = Guid.NewGuid();
        var allocations = new List<CategoryAllocationInput> { new(categoryId, 60m), new(categoryId, 40m) };
        var request = new CreateAllocationVersionRequest(500_000m, new MonthInput(2026, 9), allocations, null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_fixed_income_is_zero()
    {
        var request = new CreateAllocationVersionRequest(0m, new MonthInput(2026, 9), DefaultSplit(), null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_for_a_month_number_outside_1_to_12()
    {
        var request = new CreateAllocationVersionRequest(500_000m, new MonthInput(2026, 13), DefaultSplit(), null);

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }
}

public class OnboardingCompleteRequestValidatorTests
{
    private readonly OnboardingCompleteRequestValidator _validator = new(new FakeCurrencyCatalog());

    private static List<OnboardingCategoryInput> DefaultCategories() =>
    [
        new("Rent", "FixedAccount", 25m, null, null),
        new("Feeding", "Standard", 25m, null, null),
        new("Transportation", "Standard", 15m, null, null),
        new("Utility", "Standard", 10m, null, null),
        new("Subscription", "Standard", 5m, null, null),
        new("Investment", "FixedAccount", 20m, null, null),
    ];

    private static OnboardingCompleteRequest ValidRequest(List<OnboardingCategoryInput>? categories = null) => new(
        new CurrencyInput("USD", "$"),
        "America/New_York",
        "en-US",
        500_000m,
        new MonthInput(2026, 9),
        categories ?? DefaultCategories());

    [Fact]
    public void Passes_for_a_normal_onboarding_in_a_non_NGN_currency()
    {
        var result = _validator.Validate(ValidRequest());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Fails_for_a_currency_the_catalog_does_not_know()
    {
        var request = ValidRequest() with { Currency = new CurrencyInput("ZZZ", "?") };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_for_a_made_up_timezone()
    {
        var request = ValidRequest() with { TimeZoneId = "Not/ARealPlace" };

        var result = _validator.Validate(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_two_categories_share_a_name()
    {
        var categories = new List<OnboardingCategoryInput>
        {
            new("Rent", "FixedAccount", 50m, null, null),
            new("rent", "Standard", 50m, null, null),
        };

        var result = _validator.Validate(ValidRequest(categories));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_categories_do_not_add_up_to_100()
    {
        var categories = new List<OnboardingCategoryInput> { new("Rent", "FixedAccount", 50m, null, null) };

        var result = _validator.Validate(ValidRequest(categories));

        result.IsValid.Should().BeFalse();
    }
}
