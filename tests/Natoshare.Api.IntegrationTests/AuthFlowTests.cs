using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Natoshare.Application.Auth;
using Natoshare.Application.Me;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// This walks through the whole Phase 1 auth flow against a real running API and a
// real Postgres database: sign up, log in, call a protected endpoint, refresh the
// token, and log out.
public class AuthFlowTests : IClassFixture<NatoshareApiFactory>
{
    private readonly HttpClient _client;

    public AuthFlowTests(NatoshareApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Signup_login_refresh_protected_call_and_logout_all_work_together()
    {
        var email = $"amara+{Guid.NewGuid():N}@example.com";

        var signupResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Amara"));
        signupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var signupResult = await signupResponse.Content.ReadFromJsonAsync<AuthResult>();
        signupResult!.User.Email.Should().Be(email);
        signupResult.AccessToken.Should().NotBeNullOrWhiteSpace();
        signupResult.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, "correct-horse-1"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResult = (await loginResponse.Content.ReadFromJsonAsync<AuthResult>())!;

        // A protected endpoint works with a real access token.
        var meResponse = await SendWithToken(HttpMethod.Get, "/api/v1/me", loginResult.AccessToken);
        meResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<GetMeResult>();
        me!.User.Email.Should().Be(email);
        me.OnboardingCompleted.Should().BeFalse();

        // The same endpoint refuses a request with no token at all.
        var unauthenticated = await _client.GetAsync("/api/v1/me");
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Refreshing gives back a new access token and a new refresh token.
        var refreshResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshRequest(loginResult.RefreshToken));
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = (await refreshResponse.Content.ReadFromJsonAsync<AuthResult>())!;
        refreshed.RefreshToken.Should().NotBe(loginResult.RefreshToken);

        // The old refresh token was rotated out, it cannot be used a second time.
        var reusedOldToken = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshRequest(loginResult.RefreshToken));
        reusedOldToken.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Logging out needs a valid access token, and revokes the refresh token given.
        var logoutResponse = await SendWithToken(
            HttpMethod.Post, "/api/v1/auth/logout", refreshed.AccessToken, new RefreshRequest(refreshed.RefreshToken));
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // After logging out, that refresh token cannot get a new access token anymore.
        var afterLogout = await _client.PostAsJsonAsync(
            "/api/v1/auth/refresh", new RefreshRequest(refreshed.RefreshToken));
        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Signing_up_with_an_email_already_in_use_is_rejected()
    {
        var email = $"dupe+{Guid.NewGuid():N}@example.com";
        var request = new SignupRequest(email, "correct-horse-1", "Amara");

        var first = await _client.PostAsJsonAsync("/api/v1/auth/signup", request);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await _client.PostAsJsonAsync("/api/v1/auth/signup", request);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Signup_with_a_short_password_is_rejected_before_anything_is_created()
    {
        var email = $"short+{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/signup", new SignupRequest(email, "short", "Amara"));

        response.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Logging_in_with_the_wrong_password_is_rejected()
    {
        var email = $"wrongpass+{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Amara"));

        var login = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, "not-the-right-password"));

        login.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forgot_password_then_reset_password_lets_you_log_in_with_the_new_password()
    {
        var email = $"forgot+{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Amara"));

        var forgotResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/forgot-password", new ForgotPasswordRequest(email));
        forgotResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var body = await forgotResponse.Content.ReadFromJsonAsync<JsonElement>();
        var resetToken = body.GetProperty("resetToken").GetString();
        resetToken.Should().NotBeNullOrWhiteSpace();

        var resetResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/reset-password", new ResetPasswordRequest(email, resetToken!, "brand-new-pass-1"));
        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginWithNewPassword = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, "brand-new-pass-1"));
        loginWithNewPassword.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginWithOldPassword = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(email, "correct-horse-1"));
        loginWithOldPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Updating_your_currency_to_one_that_is_not_real_is_rejected()
    {
        var email = $"currency+{Guid.NewGuid():N}@example.com";
        var signup = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Amara"));
        var signupResult = (await signup.Content.ReadFromJsonAsync<AuthResult>())!;

        var response = await SendWithToken(
            HttpMethod.Patch, "/api/v1/me/currency", signupResult.AccessToken, new UpdateCurrencyRequest("ZZZ", "?"));

        response.StatusCode.Should().Be((HttpStatusCode)422);
    }

    private async Task<HttpResponseMessage> SendWithToken(HttpMethod method, string url, string accessToken, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await _client.SendAsync(request);
    }
}
