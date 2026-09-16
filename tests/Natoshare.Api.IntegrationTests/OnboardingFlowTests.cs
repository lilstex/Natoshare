using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Me;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks a fresh account through the whole Phase 2 flow against a real running API and
// a real Postgres database, in a currency that is not naira, to make sure Natoshare
// really does work for someone outside Nigeria, not just NGN.
public class OnboardingFlowTests : IClassFixture<NatoshareApiFactory>
{
    private readonly HttpClient _client;

    public OnboardingFlowTests(NatoshareApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Signup_seeds_default_categories_and_onboarding_state_starts_at_income()
    {
        var email = $"defaults+{Guid.NewGuid():N}@example.com";
        var signupResult = await SignupAsync(email);

        var state = await GetAsync<OnboardingStateResult>("/api/v1/onboarding/state", signupResult.AccessToken);
        state.Done.Should().BeFalse();
        state.IncomeSet.Should().BeFalse();

        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", signupResult.AccessToken);
        categories.Should().HaveCount(6);
        categories.Select(c => c.Name).Should().Contain(["Rent", "Feeding", "Transportation", "Utility", "Subscription", "Investment"]);
    }

    [Fact]
    public async Task Completing_onboarding_in_euros_saves_currency_categories_and_the_first_split()
    {
        var email = $"euro+{Guid.NewGuid():N}@example.com";
        var signupResult = await SignupAsync(email);

        var completeRequest = new OnboardingCompleteRequest(
            new CurrencyInput("EUR", "€"),
            "Europe/Berlin",
            "de-DE",
            3000m,
            new MonthInput(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            [
                new OnboardingCategoryInput("Rent", "FixedAccount", 40m, null, null),
                new OnboardingCategoryInput("Food", "Standard", 30m, null, null),
                new OnboardingCategoryInput("Savings", "FixedAccount", 30m, null, null),
            ]);

        var completeResponse = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", signupResult.AccessToken, completeRequest);
        completeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var version = (await completeResponse.Content.ReadFromJsonAsync<AllocationVersionResult>())!;
        version.FixedIncomeAmount.Should().Be(3000m);
        version.Categories.Should().HaveCount(3);
        version.Categories.First(c => c.Name == "Rent").AllocatedAmount.Should().Be(1200m);

        // A non-NGN user really does end up with their own currency and timezone, not
        // the NGN/Africa-Lagos default from signup.
        var me = await GetAsync<GetMeResult>("/api/v1/me", signupResult.AccessToken);
        me.User.CurrencyCode.Should().Be("EUR");
        me.User.TimeZoneId.Should().Be("Europe/Berlin");
        me.OnboardingCompleted.Should().BeTrue();

        var current = await GetAsync<CurrentAllocationResult>("/api/v1/allocation/current", signupResult.AccessToken);
        current.FixedIncomeAmount.Should().Be(3000m);
        current.Categories.Should().HaveCount(3);

        // The wizard replaces the signup-seeded defaults with exactly what was
        // submitted, it does not keep both sets around.
        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", signupResult.AccessToken);
        categories.Should().HaveCount(3);
        categories.Select(c => c.Name).Should().BeEquivalentTo(["Rent", "Food", "Savings"]);
    }

    [Fact]
    public async Task Completing_onboarding_a_second_time_is_rejected()
    {
        var email = $"twice+{Guid.NewGuid():N}@example.com";
        var signupResult = await SignupAsync(email);

        var request = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"),
            "UTC",
            null,
            1000m,
            new MonthInput(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            [new OnboardingCategoryInput("Everything", "Standard", 100m, null, null)]);

        var first = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", signupResult.AccessToken, request);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", signupResult.AccessToken, request);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_split_that_does_not_add_up_to_a_hundred_percent_is_rejected()
    {
        var email = $"badsplit+{Guid.NewGuid():N}@example.com";
        var signupResult = await SignupAsync(email);

        var request = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"),
            "UTC",
            null,
            1000m,
            new MonthInput(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            [
                new OnboardingCategoryInput("Rent", "Standard", 60m, null, null),
                new OnboardingCategoryInput("Food", "Standard", 30m, null, null),
            ]);

        var response = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", signupResult.AccessToken, request);
        response.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Archiving_a_category_moves_its_share_into_a_new_split_and_marks_it_archived()
    {
        var email = $"archive+{Guid.NewGuid():N}@example.com";
        var signupResult = await SignupAsync(email);

        var completeRequest = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"),
            "UTC",
            null,
            1000m,
            new MonthInput(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            [
                new OnboardingCategoryInput("Rent", "FixedAccount", 50m, null, null),
                new OnboardingCategoryInput("Fun", "Standard", 50m, null, null),
            ]);
        await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", signupResult.AccessToken, completeRequest);

        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", signupResult.AccessToken);
        var rent = categories.First(c => c.Name == "Rent");
        var fun = categories.First(c => c.Name == "Fun");

        var archiveRequest = new ArchiveCategoryRequest(
            [new CategoryAllocationInput(fun.Id, 100m)],
            null);

        var archiveResponse = await SendWithToken(HttpMethod.Post, $"/api/v1/categories/{rent.Id}/archive", signupResult.AccessToken, archiveRequest);
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var newVersion = (await archiveResponse.Content.ReadFromJsonAsync<AllocationVersionResult>())!;
        newVersion.Categories.Should().ContainSingle(c => c.CategoryId == fun.Id && c.Percentage == 100m);

        var categoriesAfter = await GetAsync<List<CategoryDto>>("/api/v1/categories?includeArchived=true", signupResult.AccessToken);
        categoriesAfter.First(c => c.Id == rent.Id).IsArchived.Should().BeTrue();
    }

    private async Task<AuthResult> SignupAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Amara"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    private async Task<T> GetAsync<T>(string url, string accessToken)
    {
        var response = await SendWithToken(HttpMethod.Get, url, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>())!;
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
