using FluentAssertions;
using Natoshare.Application.Auth;
using Xunit;

namespace Natoshare.Application.Tests.Auth;

public class SignupRequestValidatorTests
{
    private readonly SignupRequestValidator _validator = new();

    [Fact]
    public void Passes_for_a_normal_signup()
    {
        var result = _validator.Validate(new SignupRequest("amara@example.com", "correct-horse-1", "Amara"));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    public void Fails_when_the_email_is_not_real(string email)
    {
        var result = _validator.Validate(new SignupRequest(email, "correct-horse-1", "Amara"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_password_is_too_short()
    {
        var result = _validator.Validate(new SignupRequest("amara@example.com", "short", "Amara"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Fails_when_the_display_name_is_missing()
    {
        var result = _validator.Validate(new SignupRequest("amara@example.com", "correct-horse-1", ""));

        result.IsValid.Should().BeFalse();
    }
}

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _validator = new();

    [Fact]
    public void Fails_when_the_new_password_is_too_short()
    {
        var result = _validator.Validate(new ChangePasswordRequest("current-pass", "short"));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Passes_when_both_passwords_are_filled_in_properly()
    {
        var result = _validator.Validate(new ChangePasswordRequest("current-pass", "brand-new-pass-1"));

        result.IsValid.Should().BeTrue();
    }
}
