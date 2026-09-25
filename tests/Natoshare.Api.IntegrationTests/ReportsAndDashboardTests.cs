using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Dashboard;
using Natoshare.Application.Ledger;
using Natoshare.Application.Months;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Application.Reports;
using Natoshare.Infrastructure.Persistence;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the Phase 8 reports and dashboard feature against a real running API and a
// real Postgres database.
public class ReportsAndDashboardTests : IClassFixture<NatoshareApiFactory>
{
    private readonly NatoshareApiFactory _factory;
    private readonly HttpClient _client;

    public ReportsAndDashboardTests(NatoshareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Monthly_report_matches_the_real_ledger_numbers_for_a_closed_month()
    {
        var (accessToken, feeding, transportation, _, year, month) = await SetUpAccountAsync("monthly-report", 1000m);
        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Flexible", 150m, "Side hustle", null));

        // Feeding: 25% of 1000 = funded 250, spend only 100, 150 rolls to savings at close.
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(100m, "Groceries", "Category", feeding.Id, null, null, null));

        // Transportation: funded 250, overspend to 400, a 150 deficit covered from the Flexible Pool.
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(400m, "Rides", "Category", transportation.Id, null, null, null));

        await PostAsync<CloseMonthResult>(
            accessToken, $"/api/v1/months/{year}/{month}/close",
            new CloseMonthRequest(null, [new CloseDeficitResolutionInput(transportation.Id, 150m, "FlexiblePool", null, null)], null, null));

        var report = await GetAsync<MonthlyReportDto>($"/api/v1/reports/monthly?year={year}&month={month}", accessToken);

        report.IsClosed.Should().BeTrue();
        var feedingLine = report.Categories.First(c => c.CategoryId == feeding.Id);
        feedingLine.Budget.Should().Be(250m);
        feedingLine.Actual.Should().Be(100m);
        feedingLine.Saved.Should().Be(150m);
        feedingLine.Status.Should().Be("Under");

        var transportLine = report.Categories.First(c => c.CategoryId == transportation.Id);
        transportLine.Actual.Should().Be(400m);
        transportLine.Status.Should().Be("Over");

        report.DeficitsAndCoverage.Should().ContainSingle(d => d.CategoryId == transportation.Id && d.Amount == 150m && d.Method == "FlexiblePool");
        report.TotalActual.Should().Be(100m + 400m);
    }

    [Fact]
    public async Task A_month_that_never_happened_returns_an_empty_report_not_an_error()
    {
        var (accessToken, _, _, _, year, month) = await SetUpAccountAsync("empty-report", 1000m);
        var (futureYear, futureMonth) = month == 12 ? (year + 2, 1) : (year + 1, month);

        var report = await GetAsync<MonthlyReportDto>($"/api/v1/reports/monthly?year={futureYear}&month={futureMonth}", accessToken);

        report.Categories.Should().BeEmpty();
        report.TotalBudget.Should().Be(0m);
        report.IsClosed.Should().BeFalse();
    }

    [Fact]
    public async Task Range_and_annual_reports_aggregate_every_month_they_cover()
    {
        var (accessToken, feeding, _, _, year, month) = await SetUpAccountAsync("range-report", 1000m);
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(100m, "Snacks", "Category", feeding.Id, null, null, null));

        var range = await GetAsync<RangeReportDto>(
            $"/api/v1/reports/range?fromYear={year}&fromMonth={month}&toYear={year}&toMonth={month}", accessToken);
        range.Months.Should().ContainSingle();
        range.TotalActual.Should().Be(100m);

        var annual = await GetAsync<AnnualReportDto>($"/api/v1/reports/annual?year={year}", accessToken);
        annual.Months.Should().HaveCount(12);
        annual.Months.First(m => m.Month == month).TotalActual.Should().Be(100m);
    }

    [Fact]
    public async Task Csv_export_comes_back_as_a_real_csv_file_with_a_utf8_bom()
    {
        var (accessToken, feeding, _, _, year, month) = await SetUpAccountAsync("csv-export", 1000m);
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(100m, "Snacks", "Category", feeding.Id, null, null, null));

        var response = await SendWithToken(
            HttpMethod.Get, $"/api/v1/reports/export?scope=month&format=csv&year={year}&month={month}", accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        bytes.Take(3).Should().BeEquivalentTo(new byte[] { 0xEF, 0xBB, 0xBF }, options => options.WithStrictOrdering());

        var text = Encoding.UTF8.GetString(bytes);
        text.Should().Contain("Category").And.Contain("Feeding").And.Contain("100");
    }

    [Fact]
    public async Task Pdf_export_comes_back_as_a_real_pdf_file()
    {
        var (accessToken, feeding, _, _, year, month) = await SetUpAccountAsync("pdf-export", 1000m);
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(100m, "Snacks", "Category", feeding.Id, null, null, null));

        var response = await SendWithToken(
            HttpMethod.Get, $"/api/v1/reports/export?scope=month&format=pdf&year={year}&month={month}", accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    [Fact]
    public async Task Export_accepts_the_access_token_as_a_query_parameter_for_a_browser_download()
    {
        var (accessToken, _, _, _, year, month) = await SetUpAccountAsync("querytoken-export", 1000m);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/reports/export?scope=month&format=csv&year={year}&month={month}&accessToken={accessToken}");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Export_is_blocked_once_the_trial_has_lapsed()
    {
        var (accessToken, _, _, userId, year, month) = await SetUpAccountAsync("lapsed-export", 1000m);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
            var user = await dbContext.Users.FirstAsync(u => u.Id == userId);
            user.TrialEndsAt = DateTimeOffset.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();
        }

        var response = await SendWithToken(
            HttpMethod.Get, $"/api/v1/reports/export?scope=month&format=csv&year={year}&month={month}", accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Reading the report itself (not exporting it) needs no entitlement at all.
        var readResponse = await SendWithToken(HttpMethod.Get, $"/api/v1/reports/monthly?year={year}&month={month}", accessToken);
        readResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_combines_net_position_categories_and_recent_activity_in_one_call()
    {
        var (accessToken, feeding, transportation, _, _, _) = await SetUpAccountAsync("dashboard", 1000m);
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(650m, "Rides", "Category", transportation.Id, null, null, null));

        var dashboard = await GetAsync<DashboardDto>("/api/v1/dashboard", accessToken);

        dashboard.CategoryCards.Should().HaveCount(3);
        // Deficit cards sort first (see docs/06-design-system.md).
        dashboard.CategoryCards.First().CategoryId.Should().Be(transportation.Id);
        dashboard.CategoryCards.First().Deficit.Should().BeGreaterThan(0m);

        dashboard.RecentTransactions.Should().Contain(t => t.Description == "Rides" && !t.IsIncome);
        dashboard.RecentTransactions.Should().Contain(t => t.Description == "Salary" && t.IsIncome);
        dashboard.UnreadAlerts.Should().BeGreaterThanOrEqualTo(0);
        dashboard.NetPosition.Should().NotBeNull();
        dashboard.FlexiblePool.Should().NotBeNull();
    }

    [Fact]
    public async Task Obligations_only_lists_a_fixed_account_confirmation_when_it_actually_falls_inside_the_requested_window()
    {
        var (accessToken, _, _, _, _, _) = await SetUpAccountAsync("obligations-window", 1000m);

        // The Rent (FixedAccount) category is now funded but unconfirmed, its
        // obligation sits at the end of the month, which is more than a day away
        // right after signup, so a 1-day window should never include it while a
        // wide window should.
        var narrow = await GetAsync<List<ObligationItemDto>>("/api/v1/obligations?days=1", accessToken);
        var wide = await GetAsync<List<ObligationItemDto>>("/api/v1/obligations?days=60", accessToken);

        narrow.Should().NotContain(o => o.Type == "FixedAccountConfirm");
        wide.Should().Contain(o => o.Type == "FixedAccountConfirm");
    }

    private async Task<(string AccessToken, CategoryDto Feeding, CategoryDto Transportation, Guid UserId, int Year, int Month)> SetUpAccountAsync(
        string emailPrefix, decimal income)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Report Tester"));
        signupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await signupResponse.Content.ReadFromJsonAsync<AuthResult>())!;

        var now = DateTime.UtcNow;
        var onboardingRequest = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"),
            "UTC",
            "en-US",
            income,
            new MonthInput(now.Year, now.Month),
            [
                new OnboardingCategoryInput("Feeding", "Standard", 25m, null, null),
                new OnboardingCategoryInput("Transportation", "Standard", 25m, null, null),
                new OnboardingCategoryInput("Rent", "FixedAccount", 50m, null, null),
            ]);

        var onboardingResponse = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", auth.AccessToken, onboardingRequest);
        onboardingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", auth.AccessToken);
        var feeding = categories.First(c => c.Name == "Feeding");
        var transportation = categories.First(c => c.Name == "Transportation");

        await PostAsync<IncomeDto>(auth.AccessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", income, "Salary", null));

        return (auth.AccessToken, feeding, transportation, auth.User.Id, now.Year, now.Month);
    }

    private async Task<T> PostAsync<T>(string accessToken, string url, object body)
    {
        var response = await SendWithToken(HttpMethod.Post, url, accessToken, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<T> GetAsync<T>(string url, string accessToken)
    {
        var response = await SendWithToken(HttpMethod.Get, url, accessToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
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
