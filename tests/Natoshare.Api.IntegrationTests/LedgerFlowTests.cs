using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Ledger;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the whole Phase 3 core loop against a real running API and a real Postgres
// database: log income, see it split, overspend a category, resolve the deficit, and
// check the balance is right at every step.
public class LedgerFlowTests : IClassFixture<NatoshareApiFactory>
{
    private readonly HttpClient _client;

    public LedgerFlowTests(NatoshareApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Logging_income_splits_it_exactly_across_categories()
    {
        var (accessToken, categories) = await SetUpAccountAsync("split", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 30m), ("Fun", "Standard", 20m)]);

        var income = await PostAsync<IncomeDto>("/api/v1/income", accessToken,
            new LogIncomeRequest("Allocatable", 1000m, "Salary", null));

        income.Splits.Should().HaveCount(3);
        income.Splits.Sum(s => s.Amount).Should().Be(1000m);
        income.Splits.First(s => s.CategoryName == "Rent").Amount.Should().Be(500m);
        income.Splits.First(s => s.CategoryName == "Food").Amount.Should().Be(300m);
        income.Splits.First(s => s.CategoryName == "Fun").Amount.Should().Be(200m);

        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        balances.Totals.Allocated.Should().Be(1000m);
        balances.Totals.Available.Should().Be(1000m);
        balances.Totals.Deficit.Should().Be(0m);
    }

    [Fact]
    public async Task Overspending_a_category_shows_a_deficit_and_resolving_it_from_the_pool_clears_it()
    {
        var (accessToken, categories) = await SetUpAccountAsync("deficit", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 30m), ("Fun", "Standard", 20m)]);
        var fun = categories.First(c => c.Name == "Fun");

        await PostAsync<IncomeDto>("/api/v1/income", accessToken, new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<IncomeDto>("/api/v1/income", accessToken, new LogIncomeRequest("Flexible", 100m, "Gift", null));

        var expenseResponse = await PostAsync<LogExpenseResult>(
            "/api/v1/expenses", accessToken,
            new LogExpenseRequest(250m, "Concert", "Category", fun.Id, null, null, null));

        expenseResponse.WentIntoDeficit.Should().BeTrue();
        expenseResponse.Deficit.Should().NotBeNull();
        expenseResponse.Deficit!.Amount.Should().Be(50m);

        var deficits = await GetAsync<List<DeficitListItemDto>>($"/api/v1/deficits", accessToken);
        deficits.Should().ContainSingle(d => d.CategoryId == fun.Id && d.Amount == 50m);
        deficits.Single(d => d.CategoryId == fun.Id).SuggestedSources.Should().ContainSingle(s => s.Kind == "FlexiblePool" && s.AvailableToUse == 100m);

        var today = DateTime.UtcNow;
        var resolution = await PostAsync<DeficitResolutionDto>(
            "/api/v1/deficits/resolve", accessToken,
            new ResolveDeficitRequest(fun.Id, today.Year, today.Month, 50m, "FlexiblePool", null, "cover from gift"));

        resolution.Amount.Should().Be(50m);

        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        var funBalance = balances.Categories.First(c => c.CategoryId == fun.Id);
        funBalance.Deficit.Should().Be(0m);
        funBalance.Covered.Should().Be(50m);
        balances.FlexiblePool.Balance.Should().Be(50m);
    }

    [Fact]
    public async Task Editing_an_expense_reverses_the_old_amount_and_posts_the_new_one()
    {
        var (accessToken, categories) = await SetUpAccountAsync("edit", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>("/api/v1/income", accessToken, new LogIncomeRequest("Allocatable", 1000m, "Salary", null));

        var expense = await PostAsync<LogExpenseResult>(
            "/api/v1/expenses", accessToken, new LogExpenseRequest(100m, "Groceries", "Category", food.Id, null, null, null));

        var patchResponse = await SendWithToken(HttpMethod.Patch, $"/api/v1/expenses/{expense.Expense.Id}", accessToken,
            new UpdateExpenseRequest(60m, null, null, null, null, null));
        patchResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        var foodBalance = balances.Categories.First(c => c.CategoryId == food.Id);
        foodBalance.Spent.Should().Be(60m);
        foodBalance.Available.Should().Be(440m);
    }

    [Fact]
    public async Task Deleting_an_expense_reverses_it_completely()
    {
        var (accessToken, categories) = await SetUpAccountAsync("delete", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>("/api/v1/income", accessToken, new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        var expense = await PostAsync<LogExpenseResult>(
            "/api/v1/expenses", accessToken, new LogExpenseRequest(200m, "Groceries", "Category", food.Id, null, null, null));

        var deleteResponse = await SendWithToken(HttpMethod.Delete, $"/api/v1/expenses/{expense.Expense.Id}", accessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        var foodBalance = balances.Categories.First(c => c.CategoryId == food.Id);
        foodBalance.Spent.Should().Be(0m);
        foodBalance.Available.Should().Be(500m);
    }

    [Fact]
    public async Task An_idempotency_key_stops_a_retried_income_post_from_being_logged_twice()
    {
        var (accessToken, _) = await SetUpAccountAsync("idem", 1000m, [("Everything", "Standard", 100m)]);
        var key = Guid.NewGuid().ToString();

        using var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/income")
        {
            Content = JsonContent.Create(new LogIncomeRequest("Flexible", 50m, "Gift", null)),
        };
        request1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request1.Headers.Add("Idempotency-Key", key);
        var response1 = await _client.SendAsync(request1);
        var income1 = await response1.Content.ReadFromJsonAsync<IncomeDto>();

        using var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/v1/income")
        {
            Content = JsonContent.Create(new LogIncomeRequest("Flexible", 50m, "Gift", null)),
        };
        request2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request2.Headers.Add("Idempotency-Key", key);
        var response2 = await _client.SendAsync(request2);
        var income2 = await response2.Content.ReadFromJsonAsync<IncomeDto>();

        income2!.Id.Should().Be(income1!.Id);

        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        balances.FlexiblePool.Balance.Should().Be(50m);
    }

    [Fact]
    public async Task A_reallocation_that_would_leave_the_source_short_is_rejected()
    {
        var (accessToken, categories) = await SetUpAccountAsync("reallocation", 1000m, [("Rent", "FixedAccount", 50m), ("Food", "Standard", 50m)]);
        var rent = categories.First(c => c.Name == "Rent");
        var food = categories.First(c => c.Name == "Food");

        await PostAsync<IncomeDto>("/api/v1/income", accessToken, new LogIncomeRequest("Allocatable", 1000m, "Salary", null));

        var tooMuch = await SendWithToken(HttpMethod.Post, "/api/v1/reallocations", accessToken,
            new CreateReallocationRequest(new AccountRefInput("Category", rent.Id), new AccountRefInput("Category", food.Id), 999_999m, "Manual", null, null));
        tooMuch.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var justRight = await SendWithToken(HttpMethod.Post, "/api/v1/reallocations", accessToken,
            new CreateReallocationRequest(new AccountRefInput("Category", rent.Id), new AccountRefInput("Category", food.Id), 100m, "Manual", null, null));
        justRight.StatusCode.Should().Be(HttpStatusCode.OK);

        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        balances.Categories.First(c => c.CategoryId == rent.Id).Available.Should().Be(400m);
        balances.Categories.First(c => c.CategoryId == food.Id).Available.Should().Be(600m);
    }

    private async Task<(string AccessToken, List<CategoryDto> Categories)> SetUpAccountAsync(
        string emailPrefix, decimal income, (string Name, string Kind, decimal Percentage)[] categories)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Ledger Tester"));
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

    private async Task<T> PostAsync<T>(string url, string accessToken, object body)
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
