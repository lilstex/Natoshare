using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the Phase 6 people & money feature against a real running API and a real
// Postgres database: lending and getting repaid, borrowing and paying back,
// promising and partially redeeming, investment logging, and the net position and
// obligations calendar built on top of all of it.
public class PeopleAndMoneyTests : IClassFixture<NatoshareApiFactory>
{
    private readonly HttpClient _client;

    public PeopleAndMoneyTests(NatoshareApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Lending_from_a_category_and_getting_repaid_moves_real_money_and_settles_the_loan()
    {
        var (accessToken, feeding) = await SetUpAccountAsync("loans", 1000m);

        var loan = await PostAsync<LoanOutDto>(accessToken, "/api/v1/loans-out", new CreateLoanOutRequest(
            "Tunde", 200m, new DateOnly(2026, 9, 17), null, "emergency", new AccountRefInput("Category", feeding.Id)));

        loan.Status.Should().Be("Outstanding");
        loan.LinkedSource!.Kind.Should().Be("Category");

        // Feeding is funded 50% of 1000 = 500, lending 200 out of it should leave 300.
        var balancesAfterLending = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        balancesAfterLending.Categories.First(c => c.CategoryId == feeding.Id).Funded.Should().Be(300m);

        var afterRepayment = await PostAsync<LoanOutDto>(
            accessToken, $"/api/v1/loans-out/{loan.Id}/repayments",
            new CreateLoanRepaymentRequest(200m, new DateOnly(2026, 9, 18), null, new AccountRefInput("FlexiblePool", null)));

        afterRepayment.Status.Should().Be("Repaid");
        afterRepayment.Outstanding.Should().Be(0m);
        afterRepayment.Repayments.Should().HaveCount(1);

        var balancesAfterRepayment = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        balancesAfterRepayment.FlexiblePool.Balance.Should().Be(200m);

        // A settled loan with repayments already on it cannot be deleted any more.
        var deleteResponse = await SendWithToken(HttpMethod.Delete, $"/api/v1/loans-out/{loan.Id}", accessToken);
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Lending_more_than_a_category_has_funded_is_rejected()
    {
        var (accessToken, feeding) = await SetUpAccountAsync("overlend", 1000m);

        var response = await SendWithToken(HttpMethod.Post, "/api/v1/loans-out", accessToken, new CreateLoanOutRequest(
            "Tunde", 999m, new DateOnly(2026, 9, 17), null, null, new AccountRefInput("Category", feeding.Id)));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task A_debt_repaid_from_a_category_reduces_that_categorys_funded_amount_and_settles_the_debt()
    {
        var (accessToken, feeding) = await SetUpAccountAsync("debts", 1000m);

        var debt = await PostAsync<DebtInDto>(
            accessToken, "/api/v1/debts", new CreateDebtInRequest("Ada", 150m, new DateOnly(2026, 9, 17), new DateOnly(2026, 9, 30), null));
        debt.Status.Should().Be("Outstanding");

        var afterRepayment = await PostAsync<DebtInDto>(
            accessToken, $"/api/v1/debts/{debt.Id}/repayments",
            new CreateDebtRepaymentRequest(150m, new DateOnly(2026, 9, 18), null, new AccountRefInput("Category", feeding.Id)));

        afterRepayment.Status.Should().Be("Repaid");

        // Feeding is funded 50% of 1000 = 500, paying back 150 out of it should leave 350.
        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        balances.Categories.First(c => c.CategoryId == feeding.Id).Funded.Should().Be(350m);

        // A debt's status is always computed from repayments, never set by hand.
        var patchResponse = await SendWithToken(
            HttpMethod.Patch, $"/api/v1/debts/{debt.Id}", accessToken, new UpdateDebtInRequest(null, null, null, "Outstanding"));
        patchResponse.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task A_promise_partially_redeemed_twice_adds_up_correctly_and_settles_on_the_second_redemption()
    {
        var (accessToken, _) = await SetUpAccountAsync("promises", 1000m);
        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Flexible", 100m, "Side hustle", null));

        var promise = await PostAsync<PromiseDto>(
            accessToken, "/api/v1/promises", new CreatePromiseRequest("Chidi", 100m, "birthday gift", new DateOnly(2026, 9, 17)));
        promise.Status.Should().Be("Open");

        var afterFirst = await PostAsync<PromiseDto>(
            accessToken, $"/api/v1/promises/{promise.Id}/redemptions",
            new CreatePromiseRedemptionRequest(40m, new DateOnly(2026, 9, 18), new AccountRefInput("FlexiblePool", null), null));

        afterFirst.Status.Should().Be("PartiallyRedeemed");
        afterFirst.TotalRedeemed.Should().Be(40m);
        afterFirst.Outstanding.Should().Be(60m);

        var afterSecond = await PostAsync<PromiseDto>(
            accessToken, $"/api/v1/promises/{promise.Id}/redemptions",
            new CreatePromiseRedemptionRequest(60m, new DateOnly(2026, 9, 19), new AccountRefInput("FlexiblePool", null), null));

        afterSecond.Status.Should().Be("Redeemed");
        afterSecond.Outstanding.Should().Be(0m);
        afterSecond.Redemptions.Should().HaveCount(2);

        var poolBalance = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        poolBalance.FlexiblePool.Balance.Should().Be(0m);

        // Fully redeemed, cannot be redeemed against again.
        var overRedeemResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/promises/{promise.Id}/redemptions", accessToken,
            new CreatePromiseRedemptionRequest(1m, new DateOnly(2026, 9, 20), new AccountRefInput("FlexiblePool", null), null));
        overRedeemResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Net_position_combines_savings_pool_loans_debts_and_promises_the_way_the_formula_says()
    {
        var (accessToken, feeding) = await SetUpAccountAsync("networth", 1000m);
        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Flexible", 300m, "Side hustle", null));

        await PostAsync<LoanOutDto>(accessToken, "/api/v1/loans-out", new CreateLoanOutRequest(
            "Tunde", 50m, new DateOnly(2026, 9, 17), null, null, new AccountRefInput("Category", feeding.Id)));
        await PostAsync<DebtInDto>(
            accessToken, "/api/v1/debts", new CreateDebtInRequest("Ada", 30m, new DateOnly(2026, 9, 17), null, null));
        await PostAsync<PromiseDto>(
            accessToken, "/api/v1/promises", new CreatePromiseRequest("Chidi", 20m, null, new DateOnly(2026, 9, 17)));

        var netPosition = await GetAsync<NetPositionDto>("/api/v1/net-position", accessToken);

        // Pool: 300 flexible income logged. LoansOut: 50 outstanding. DebtsIn: 30
        // outstanding. OpenPromises: 20 open. Nothing saved or deployed yet, no
        // carried deficit this month.
        netPosition.Breakdown.Pool.Should().Be(300m);
        netPosition.Breakdown.LoansOut.Should().Be(50m);
        netPosition.Breakdown.DebtsIn.Should().Be(30m);
        netPosition.Breakdown.OpenPromises.Should().Be(20m);
        netPosition.Total.Should().Be(300m + 50m - 30m - 20m);
    }

    [Fact]
    public async Task Obligations_lists_a_debt_coming_due_but_not_one_already_repaid()
    {
        var (accessToken, _) = await SetUpAccountAsync("obligations", 1000m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var debt = await PostAsync<DebtInDto>(
            accessToken, "/api/v1/debts", new CreateDebtInRequest("Ada", 30m, today, today.AddDays(5), null));

        var beforeRepayment = await GetAsync<List<ObligationItemDto>>("/api/v1/obligations?days=30", accessToken);
        beforeRepayment.Should().Contain(o => o.Type == "DebtDue" && o.EntityId == debt.Id);

        await PostAsync<DebtInDto>(
            accessToken, $"/api/v1/debts/{debt.Id}/repayments",
            new CreateDebtRepaymentRequest(30m, today, null, null));

        var afterRepayment = await GetAsync<List<ObligationItemDto>>("/api/v1/obligations?days=30", accessToken);
        afterRepayment.Should().NotContain(o => o.Type == "DebtDue" && o.EntityId == debt.Id);
    }

    [Fact]
    public async Task Investment_summary_compares_what_was_allocated_against_what_was_actually_invested()
    {
        var (accessToken, _) = await SetUpAccountAsync("investing", 1000m);
        var now = DateTime.UtcNow;

        await PostAsync<InvestmentLogDto>(
            accessToken, "/api/v1/investments", new CreateInvestmentLogRequest(120m, new DateOnly(now.Year, now.Month, 1), "PiggyVest", null));

        var summary = await GetAsync<InvestmentSummaryDto>($"/api/v1/investments/summary?year={now.Year}&month={now.Month}", accessToken);

        // The default "Everyday" split gives Investment 20% of a 1000 income, so 200
        // is allocated, only 120 of it was actually logged as invested.
        summary.Allocated.Should().Be(200m);
        summary.Invested.Should().Be(120m);
        summary.Shortfall.Should().Be(80m);
    }

    private async Task<(string AccessToken, CategoryDto Feeding)> SetUpAccountAsync(string emailPrefix, decimal income)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "PnM Tester"));
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
                new OnboardingCategoryInput("Feeding", "Standard", 50m, null, null),
                new OnboardingCategoryInput("Investment", "FixedAccount", 20m, null, null),
                new OnboardingCategoryInput("Rent", "FixedAccount", 30m, null, null),
            ]);

        var onboardingResponse = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", auth.AccessToken, onboardingRequest);
        onboardingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", auth.AccessToken);
        var feeding = categories.First(c => c.Name == "Feeding");

        await PostAsync<IncomeDto>(auth.AccessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", income, "Salary", null));

        return (auth.AccessToken, feeding);
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
