using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Ledger;
using Natoshare.Application.Notifications;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the Phase 4 alerting loop against a real running API and a real Postgres:
// a new account already has sensible alert preferences, an overspending expense
// raises exactly the alerts it should (not duplicated by a second write), and turning
// an alert off actually stops it firing.
public class AlertsAndNotificationsTests : IClassFixture<NatoshareApiFactory>
{
    private readonly HttpClient _client;

    public AlertsAndNotificationsTests(NatoshareApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_new_account_already_has_every_currently_evaluated_alert_preference_enabled()
    {
        var (accessToken, _) = await SetUpAccountAsync("prefs", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);

        var preferences = await GetAsync<List<AlertPreferenceDto>>("/api/v1/notifications/preferences", accessToken);

        preferences.Should().HaveCount(8);
        preferences.Should().OnlyContain(p => p.Enabled);
        preferences.Select(p => p.Kind).Should().BeEquivalentTo([
            "OverPaceCategory", "OverspendCategory", "CategoryInDeficit", "SafeToSpendLow",
            "MonthCloseReminder", "FixedAccountUnconfirmed", "CarriedDeficitApplied", "MonthEndSummary",
        ]);
    }

    [Fact]
    public async Task Overspending_a_category_creates_exactly_one_overspend_alert_and_one_deficit_alert()
    {
        var (accessToken, categories) = await SetUpAccountAsync("overspend", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(600m, "Big shop", "Category", food.Id, null, null, null));

        var overspendAlerts = await GetAsync<List<NotificationDto>>("/api/v1/notifications?kind=OverspendCategory", accessToken);
        var deficitAlerts = await GetAsync<List<NotificationDto>>("/api/v1/notifications?kind=CategoryInDeficit", accessToken);

        overspendAlerts.Should().ContainSingle();
        deficitAlerts.Should().ContainSingle();
        overspendAlerts[0].IsRead.Should().BeFalse();

        // A second expense that keeps the category in deficit should not duplicate
        // either alert, OverspendCategory fires once ever per month, and
        // CategoryInDeficit is deduplicated to once a day.
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(50m, "More shopping", "Category", food.Id, null, null, null));

        var overspendAlertsAfter = await GetAsync<List<NotificationDto>>("/api/v1/notifications?kind=OverspendCategory", accessToken);
        var deficitAlertsAfter = await GetAsync<List<NotificationDto>>("/api/v1/notifications?kind=CategoryInDeficit", accessToken);

        overspendAlertsAfter.Should().ContainSingle();
        deficitAlertsAfter.Should().ContainSingle();
    }

    [Fact]
    public async Task Turning_off_an_alert_kind_stops_it_firing()
    {
        var (accessToken, categories) = await SetUpAccountAsync("toggle", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        var disableResponse = await SendWithToken(HttpMethod.Patch, "/api/v1/notifications/preferences", accessToken,
            new List<UpdateAlertPreferenceInput> { new("OverspendCategory", false, null, null) });
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(600m, "Big shop", "Category", food.Id, null, null, null));

        var overspendAlerts = await GetAsync<List<NotificationDto>>("/api/v1/notifications?kind=OverspendCategory", accessToken);
        overspendAlerts.Should().BeEmpty();

        // The one alert kind that was not touched should still fire normally.
        var deficitAlerts = await GetAsync<List<NotificationDto>>("/api/v1/notifications?kind=CategoryInDeficit", accessToken);
        deficitAlerts.Should().ContainSingle();
    }

    [Fact]
    public async Task Marking_a_notification_read_and_marking_all_read_both_work()
    {
        var (accessToken, categories) = await SetUpAccountAsync("markread", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(600m, "Big shop", "Category", food.Id, null, null, null));

        var unread = await GetAsync<List<NotificationDto>>("/api/v1/notifications?unreadOnly=true", accessToken);
        unread.Should().NotBeEmpty();

        var readOneResponse = await SendWithToken(HttpMethod.Post, $"/api/v1/notifications/{unread[0].Id}/read", accessToken);
        readOneResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var readAllResponse = await SendWithToken(HttpMethod.Post, "/api/v1/notifications/read-all", accessToken);
        readAllResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var unreadAfter = await GetAsync<List<NotificationDto>>("/api/v1/notifications?unreadOnly=true", accessToken);
        unreadAfter.Should().BeEmpty();
    }

    private async Task<(string AccessToken, List<CategoryDto> Categories)> SetUpAccountAsync(
        string emailPrefix, decimal income, (string Name, string Kind, decimal Percentage)[] categories)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Alerts Tester"));
        signupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await signupResponse.Content.ReadFromJsonAsync<AuthResult>())!;

        var onboardingRequest = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"),
            "UTC",
            "en-US",
            income,
            new MonthInput(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            categories.Select(c => new OnboardingCategoryInput(c.Name, c.Kind, c.Percentage, null, null)).ToList());

        var onboardingResponse = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", auth.AccessToken, onboardingRequest);
        onboardingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var categoryDtos = await GetAsync<List<CategoryDto>>("/api/v1/categories", auth.AccessToken);
        return (auth.AccessToken, categoryDtos);
    }

    private async Task<T> PostAsync<T>(string accessToken, string url, object body)
    {
        var response = await SendWithToken(HttpMethod.Post, url, accessToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>())!;
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
