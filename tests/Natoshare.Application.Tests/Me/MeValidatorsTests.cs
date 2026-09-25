using FluentAssertions;
using Natoshare.Application.Common;
using Natoshare.Application.Me;
using Xunit;

namespace Natoshare.Application.Tests.Me;

// A tiny stand-in for the real currency catalog, just so we can test the validator on
// its own without needing the real ISO 4217 lookup.
file sealed class FakeCurrencyCatalog : ICurrencyCatalog
{
    public bool IsValidCode(string code) => code is "NGN" or "USD" or "EUR";

    public IReadOnlyList<CurrencyOption> GetCommonCurrencies() =>
        [new CurrencyOption("NGN", "₦", "Nigerian Naira")];
}

public class UpdateMeRequestValidatorTests
{
    private readonly UpdateMeRequestValidator _validator = new();

    [Fact]
    public void Passes_when_nothing_is_being_changed()
    {
        var result = _validator.Validate(new UpdateMeRequest(null, null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Passes_for_a_real_timezone()
    {
        var result = _validator.Validate(new UpdateMeRequest(null, "Africa/Lagos", null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Fails_for_a_made_up_timezone()
    {
        var result = _validator.Validate(new UpdateMeRequest(null, "Not/ARealPlace", null));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_for_a_made_up_locale()
    {
        var result = _validator.Validate(new UpdateMeRequest(null, null, "not-a-locale-at-all"));

        result.IsValid.Should().BeFalse();
    }
}

public class UpdateCurrencyRequestValidatorTests
{
    private readonly UpdateCurrencyRequestValidator _validator = new(new FakeCurrencyCatalog());

    [Fact]
    public void Passes_for_a_currency_the_catalog_knows_about()
    {
        var result = _validator.Validate(new UpdateCurrencyRequest("USD", "$"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Fails_for_a_currency_the_catalog_does_not_know_about()
    {
        var result = _validator.Validate(new UpdateCurrencyRequest("ZZZ", "?"));

        result.IsValid.Should().BeFalse();
    }
}

public class DeleteAccountRequestValidatorTests
{
    private readonly DeleteAccountRequestValidator _validator = new();

    [Fact]
    public void Fails_when_the_confirm_text_is_wrong()
    {
        var result = _validator.Validate(new DeleteAccountRequest("my-password", "delete"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Passes_when_the_confirm_text_matches_exactly()
    {
        var result = _validator.Validate(new DeleteAccountRequest("my-password", "DELETE"));

        result.IsValid.Should().BeTrue();
    }
}
