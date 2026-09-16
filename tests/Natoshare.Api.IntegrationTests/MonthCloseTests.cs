using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Ledger;
using Natoshare.Application.Me;
using Natoshare.Application.Months;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the Phase 5 close-month ritual against a real running API and a real
// Postgres database, following the same numbers as the worked example in
// 01-domain-model.md §5 as closely as a single calendar month allows: a category
// that saves, one covered from another category's savings, and one carried forward
// to next month, then checks next month actually starts with those numbers applied.
public class MonthCloseTests : IClassFixture<NatoshareApiFactory>
{
    private readonly HttpClient _client;

    public MonthCloseTests(NatoshareApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Closing_is_blocked_while_a_category_still_has_an_unresolved_deficit()
    {
        var (accessToken, categories, year, month) = await SetUpMonthAsync(
            "blocked", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(600m, "Overspend", "Category", food.Id, null, null, null));

        var closeResponse = await SendWithToken(HttpMethod.Post, $"/api/v1/months/{year}/{month}/close", accessToken,
            new CloseMonthRequest(null, [], null, null));

        closeResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_category_that_underspends_rolls_its_leftover_into_savings_at_close()
    {
        var (accessToken, categories, year, month) = await SetUpMonthAsync(
            "savings", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(300m, "Groceries", "Category", food.Id, null, null, null));

        // Food is funded 500, spent 300, so 200 should roll into savings.
        var closeResult = await PostAsync<CloseMonthResult>(
            accessToken, $"/api/v1/months/{year}/{month}/close", new CloseMonthRequest(null, [], null, null));

        closeResult.Status.Should().Be("Closed");
        var foodDetail = closeResult.Categories.First(c => c.CategoryId == food.Id);
        foodDetail.SavedThisMonth.Should().Be(200m);
        foodDetail.CarriedOutSavings.Should().Be(200m);
        foodDetail.Deficit.Should().Be(0m);

        // Next month's live preview (nothing materialised yet) should already show
        // that 200 pulled back in, and actually opening it for real should match.
        var nextMonth = NextMonth(year, month);
        var previewDetail = await GetAsync<MonthDetailDto>($"/api/v1/months/{nextMonth.Year}/{nextMonth.Month}", accessToken);
        previewDetail.Categories.First(c => c.CategoryId == food.Id).CarriedInSavings.Should().Be(200m);

        var opened = await PostAsync<MonthDetailDto>(accessToken, $"/api/v1/months/{nextMonth.Year}/{nextMonth.Month}/open", new { });
        opened.Categories.First(c => c.CategoryId == food.Id).CarriedInSavings.Should().Be(200m);
    }

    [Fact]
    public async Task A_deficit_covered_from_another_categorys_savings_nets_to_zero_and_reduces_that_categorys_carried_out_savings()
    {
        var (accessToken, categories, year, month) = await SetUpMonthAsync(
            "othersavings", 1000m, [("Feeding", "Standard", 50m), ("Transportation", "Standard", 50m)]);
        var feeding = categories.First(c => c.Name == "Feeding");
        var transportation = categories.First(c => c.Name == "Transportation");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));

        // Feeding: funded 500, spend only 300, so it will have 200 in savings by close.
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(300m, "Groceries", "Category", feeding.Id, null, null, null));

        // Transportation: funded 500, overspend to 650, a deficit of 150.
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(650m, "Rides", "Category", transportation.Id, null, null, null));

        var closeResult = await PostAsync<CloseMonthResult>(
            accessToken, $"/api/v1/months/{year}/{month}/close",
            new CloseMonthRequest(
                null,
                [new CloseDeficitResolutionInput(transportation.Id, 150m, "OtherCategorySavings", feeding.Id, "cover from feeding")],
                null,
                null));

        var transportDetail = closeResult.Categories.First(c => c.CategoryId == transportation.Id);
        transportDetail.Deficit.Should().Be(0m);
        transportDetail.DeficitAtClose.Should().Be(150m);
        transportDetail.DeficitResolvedVia.Should().Be("OtherCategorySavings");

        var feedingDetail = closeResult.Categories.First(c => c.CategoryId == feeding.Id);
        // Feeding would have saved 200, but 150 of it went to cover Transportation.
        feedingDetail.CarriedOutSavings.Should().Be(50m);
    }

    [Fact]
    public async Task A_deficit_carried_to_next_month_nets_this_month_to_zero_and_reduces_next_months_funding()
    {
        var (accessToken, categories, year, month) = await SetUpMonthAsync(
            "carryforward", 1000m, [("Utility", "Standard", 50m), ("Rent", "FixedAccount", 50m)]);
        var utility = categories.First(c => c.Name == "Utility");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(540m, "Big bill", "Category", utility.Id, null, null, null));

        // Utility: funded 500, spent 540, deficit 40, nothing anywhere to cover it
        // from, carry it to next month instead.
        var closeResult = await PostAsync<CloseMonthResult>(
            accessToken, $"/api/v1/months/{year}/{month}/close",
            new CloseMonthRequest(
                null, [new CloseDeficitResolutionInput(utility.Id, 40m, "NextMonthAllocation", null, null)], null, null));

        var utilityDetail = closeResult.Categories.First(c => c.CategoryId == utility.Id);
        utilityDetail.DeficitAtClose.Should().Be(40m);
        utilityDetail.CarriedOutDeficit.Should().Be(40m);
        utilityDetail.DeficitResolvedVia.Should().Be("NextMonthAllocation");

        var nextMonth = NextMonth(year, month);
        var opened = await PostAsync<MonthDetailDto>(accessToken, $"/api/v1/months/{nextMonth.Year}/{nextMonth.Month}/open", new { });
        var nextUtility = opened.Categories.First(c => c.CategoryId == utility.Id);

        nextUtility.CarriedInDeficit.Should().Be(40m);
    }

    [Fact]
    public async Task Confirming_a_fixed_account_deploys_it_externally_and_it_has_nothing_left_to_save_at_close()
    {
        var (accessToken, categories, year, month) = await SetUpMonthAsync(
            "fixedaccount", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var rent = categories.First(c => c.Name == "Rent");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));

        var confirmResponse = await SendWithToken(HttpMethod.Post, $"/api/v1/months/{year}/{month}/confirm-fixed-account", accessToken,
            new ConfirmFixedAccountRequest(rent.Id, 500m, new DateOnly(year, month, 28)));
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var closeResult = await PostAsync<CloseMonthResult>(
            accessToken, $"/api/v1/months/{year}/{month}/close", new CloseMonthRequest(null, [], null, null));

        var rentDetail = closeResult.Categories.First(c => c.CategoryId == rent.Id);
        rentDetail.SavedThisMonth.Should().Be(0m);
        rentDetail.ExternalTransferConfirmed.Should().BeTrue();
        rentDetail.ExternalTransferAmount.Should().Be(500m);
    }

    [Fact]
    public async Task A_closed_month_rejects_further_writes_and_the_admin_reopen_path_puts_it_back_to_open()
    {
        var (accessToken, categories, year, month) = await SetUpMonthAsync(
            "reopen", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<CloseMonthResult>(accessToken, $"/api/v1/months/{year}/{month}/close", new CloseMonthRequest(null, [], null, null));

        var reCloseResponse = await SendWithToken(HttpMethod.Post, $"/api/v1/months/{year}/{month}/close", accessToken,
            new CloseMonthRequest(null, [], null, null));
        reCloseResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Nothing can be logged against a closed month, whether it is brand new or
        // an edit to something that already happened in it.
        var newExpenseResponse = await SendWithToken(HttpMethod.Post, "/api/v1/expenses", accessToken,
            new LogExpenseRequest(10m, "Should not work", "Category", food.Id, null, null, null));
        newExpenseResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var incomeInClosedMonth = await GetAsync<List<IncomeDto>>("/api/v1/income", accessToken);
        var editResponse = await SendWithToken(HttpMethod.Patch, $"/api/v1/income/{incomeInClosedMonth[0].Id}", accessToken,
            new UpdateIncomeRequest(1100m, null, null));
        editResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var userDetailResponse = await GetAsync<MonthDetailDto>($"/api/v1/months/{year}/{month}", accessToken);
        userDetailResponse.Status.Should().Be("Closed");

        var meResponse = await GetAsync<GetMeResult>("/api/v1/me", accessToken);

        // An ordinary user cannot reopen their own month, only an admin can.
        var deniedReopenResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/admin/months/{meResponse.User.Id}/{year}/{month}/reopen", accessToken,
            new ReopenMonthRequest("Should not be allowed"));
        deniedReopenResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminEmail = "admin@natoshare.test";
        var adminLoginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(adminEmail, "NatoshareAdmin1"));
        adminLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var adminAuth = (await adminLoginResponse.Content.ReadFromJsonAsync<AuthResult>())!;

        var reopenResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/admin/months/{meResponse.User.Id}/{year}/{month}/reopen", adminAuth.AccessToken,
            new ReopenMonthRequest("Investigating a support ticket"));
        reopenResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var afterReopen = await GetAsync<MonthDetailDto>($"/api/v1/months/{year}/{month}", accessToken);
        afterReopen.Status.Should().Be("Open");

        // Now that it is open again, logging a fresh expense should work fine.
        var expenseAfterReopen = await SendWithToken(HttpMethod.Post, "/api/v1/expenses", accessToken,
            new LogExpenseRequest(10m, "After reopen", "Category", food.Id, null, null, null));
        expenseAfterReopen.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<(string AccessToken, List<CategoryDto> Categories, int Year, int Month)> SetUpMonthAsync(
        string emailPrefix, decimal income, (string Name, string Kind, decimal Percentage)[] categories)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Close Tester"));
        signupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await signupResponse.Content.ReadFromJsonAsync<AuthResult>())!;

        var now = DateTime.UtcNow;
        var onboardingRequest = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"),
            "UTC",
            "en-US",
            income,
            new MonthInput(now.Year, now.Month),
            categories.Select(c => new OnboardingCategoryInput(c.Name, c.Kind, c.Percentage, null, null)).ToList());

        var onboardingResponse = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", auth.AccessToken, onboardingRequest);
        onboardingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var categoryDtos = await GetAsync<List<CategoryDto>>("/api/v1/categories", auth.AccessToken);
        return (auth.AccessToken, categoryDtos, now.Year, now.Month);
    }

    private static (int Year, int Month) NextMonth(int year, int month) => month == 12 ? (year + 1, 1) : (year, month + 1);

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
