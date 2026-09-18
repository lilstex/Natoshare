using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.Maintenance;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Application.Planning;
using Natoshare.Application.Subscriptions;
using Natoshare.Infrastructure.Persistence;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the Phase 9 subscription tiers and gating feature against a real running
// API and a real Postgres database: every gated endpoint returns upgrade-required
// on Free, downgrading locks things without losing data, and upgrading again
// restores everything, matching this phase's own Verify criteria exactly.
public class SubscriptionsAndGatingTests : IClassFixture<NatoshareApiFactory>
{
    private readonly NatoshareApiFactory _factory;
    private readonly HttpClient _client;

    public SubscriptionsAndGatingTests(NatoshareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_trial_account_sees_full_pro_entitlements()
    {
        var (accessToken, _, _) = await SetUpAccountAsync("trial-entitlements", ["A", "B"]);

        var entitlements = await GetAsync<PlanEntitlements>("/api/v1/me/entitlements", accessToken);

        entitlements.Plan.Should().Be("Pro");
        entitlements.IsTrial.Should().BeTrue();
        entitlements.MaxCategories.Should().BeNull();
        entitlements.HistoryWindowDays.Should().BeNull();
        entitlements.SinkingFund.Should().BeTrue();
        entitlements.DeficitCoverFromSavings.Should().BeTrue();
        entitlements.Recurring.Should().BeTrue();
        entitlements.Export.Should().BeTrue();
    }

    [Fact]
    public async Task A_lapsed_trial_with_more_categories_than_the_free_limit_locks_only_the_extras_by_sort_order()
    {
        var (accessToken, categories, userId) = await SetUpAccountAsync("lock-order", ["A", "B", "C", "D", "E"]);
        await ExpireTrialAsync(userId);

        var afterLapse = await GetAsync<List<CategoryDto>>("/api/v1/categories", accessToken);

        afterLapse.Where(c => c.Name is "A" or "B" or "C" or "D").Should().OnlyContain(c => !c.IsLocked);
        afterLapse.First(c => c.Name == "E").IsLocked.Should().BeTrue();

        var lockedCategory = afterLapse.First(c => c.Name == "E");
        var expenseResponse = await SendWithToken(HttpMethod.Post, "/api/v1/expenses", accessToken, new LogExpenseRequest(
            10m, "Should be blocked", "Category", lockedCategory.Id, null, null, null));
        expenseResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await expenseResponse.Content.ReadAsStringAsync()).Should().Contain("upgrade-required");

        // Data is kept, not deleted: the category itself is still readable and its
        // name can still be changed, only new spending against it is refused.
        var renameResponse = await SendWithToken(
            HttpMethod.Patch, $"/api/v1/categories/{lockedCategory.Id}", accessToken, new UpdateCategoryRequest("E renamed", null, null));
        renameResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_lapsed_free_account_cannot_add_a_category_past_the_limit()
    {
        var (accessToken, _, userId) = await SetUpAccountAsync("category-limit", ["A", "B", "C", "D"]);
        await ExpireTrialAsync(userId);

        var response = await SendWithToken(HttpMethod.Post, "/api/v1/categories", accessToken, new CreateCategoryRequest("E", "Standard", null, null));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_lapsed_free_account_cannot_cover_a_deficit_from_savings_but_can_still_use_the_flexible_pool()
    {
        var (accessToken, categories, userId) = await SetUpAccountAsync("deficit-gate", ["A", "B"]);
        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Flexible", 200m, "Side hustle", null));

        var categoryA = categories.First(c => c.Name == "A");
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(600m, "Overspend", "Category", categoryA.Id, null, null, null));

        await ExpireTrialAsync(userId);

        var now = DateTime.UtcNow;
        var ownSavingsResponse = await SendWithToken(
            HttpMethod.Post, "/api/v1/deficits/resolve", accessToken,
            new ResolveDeficitRequest(categoryA.Id, now.Year, now.Month, 100m, "OwnSavings", null, null));
        ownSavingsResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var deficits = await GetAsync<List<DeficitListItemDto>>($"/api/v1/deficits?year={now.Year}&month={now.Month}", accessToken);
        deficits.First(d => d.CategoryId == categoryA.Id).SuggestedSources
            .Should().NotContain(s => s.Kind == "OwnSavings" || s.Kind == "OtherCategorySavings");

        var poolResponse = await SendWithToken(
            HttpMethod.Post, "/api/v1/deficits/resolve", accessToken,
            new ResolveDeficitRequest(categoryA.Id, now.Year, now.Month, 100m, "FlexiblePool", null, null));
        poolResponse.StatusCode.Should().Be(HttpStatusCode.OK, await poolResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_lapsed_free_account_gets_no_carried_in_savings_available_reverts_to_allocation_minus_spend()
    {
        var (accessToken, categories, userId) = await SetUpAccountAsync("sinking-fund-gate", ["A", "B"]);
        var now = DateTime.UtcNow;
        var categoryA = categories.First(c => c.Name == "A");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(100m, "Small spend", "Category", categoryA.Id, null, null, null));

        var closeResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/months/{now.Year}/{now.Month}/close", accessToken,
            new { fixedAccountConfirmations = (object?)null, deficitResolutions = Array.Empty<object>(), promiseRedemptions = (object?)null, rebalances = (object?)null });
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await closeResponse.Content.ReadAsStringAsync());

        await ExpireTrialAsync(userId);

        var nextMonth = now.Month == 12 ? new DateTime(now.Year + 1, 1, 1) : new DateTime(now.Year, now.Month + 1, 1);
        var opened = await SendWithToken(HttpMethod.Post, $"/api/v1/months/{nextMonth.Year}/{nextMonth.Month}/open", accessToken, new { });
        opened.StatusCode.Should().Be(HttpStatusCode.OK, await opened.Content.ReadAsStringAsync());

        var balances = await GetAsync<BalancesResult>("/api/v1/balances", accessToken);
        // Next month's Feeding-equivalent category should have carried in nothing,
        // "available" is just this month's own allocation.
        balances.Categories.First(c => c.CategoryId == categoryA.Id).CarriedInSavings.Should().Be(0m);
    }

    [Fact]
    public async Task The_transaction_history_window_clamps_a_free_accounts_expense_list()
    {
        var (accessToken, categories, userId) = await SetUpAccountAsync("history-window", ["A", "B"]);
        var categoryA = categories.First(c => c.Name == "A");

        await PostAsync<IncomeDto>(accessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", 1000m, "Salary", null));
        var oldDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-90));
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(50m, "Old expense", "Category", categoryA.Id, null, oldDate, null));

        await ExpireTrialAsync(userId);

        // Explicitly asking for everything back to that old date should still be
        // clamped to the 60 day window once the account is Free.
        var response = await GetAsync<List<ExpenseDto>>($"/api/v1/expenses?from={oldDate:yyyy-MM-dd}", accessToken);

        response.Should().NotContain(e => e.Description == "Old expense");
    }

    [Fact]
    public async Task The_recurring_items_and_export_features_are_blocked_on_free_but_come_back_after_upgrading()
    {
        var (accessToken, _, userId) = await SetUpAccountAsync("recurring-export-gate", ["A", "B"]);
        await ExpireTrialAsync(userId);

        var recurringResponse = await SendWithToken(HttpMethod.Get, "/api/v1/recurring", accessToken);
        recurringResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var now = DateTime.UtcNow;
        var exportResponse = await SendWithToken(
            HttpMethod.Get, $"/api/v1/reports/export?scope=month&format=csv&year={now.Year}&month={now.Month}", accessToken);
        exportResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await UpgradeAndActivateAsync(accessToken);

        var recurringAfterUpgrade = await SendWithToken(HttpMethod.Get, "/api/v1/recurring", accessToken);
        recurringAfterUpgrade.StatusCode.Should().Be(HttpStatusCode.OK);

        var exportAfterUpgrade = await SendWithToken(
            HttpMethod.Get, $"/api/v1/reports/export?scope=month&format=csv&year={now.Year}&month={now.Month}", accessToken);
        exportAfterUpgrade.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Obligations_are_clamped_to_a_basic_window_on_free_but_not_on_a_trial()
    {
        var (accessToken, _, userId) = await SetUpAccountAsync("obligations-basic", ["A", "B"]);

        var debtResponse = await PostAsync<System.Text.Json.JsonElement>(
            accessToken, "/api/v1/debts",
            new { lenderName = "Ada", amount = 30m, borrowedOn = DateOnly.FromDateTime(DateTime.UtcNow), dueOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)), note = (string?)null });

        var wideBeforeExpiry = await GetAsync<List<ObligationItemDto>>("/api/v1/obligations?days=30", accessToken);
        wideBeforeExpiry.Should().Contain(o => o.Type == "DebtDue");

        await ExpireTrialAsync(userId);

        var wideAfterExpiry = await GetAsync<List<ObligationItemDto>>("/api/v1/obligations?days=30", accessToken);
        wideAfterExpiry.Should().NotContain(o => o.Type == "DebtDue");
    }

    [Fact]
    public async Task The_upgrade_flow_creates_a_pending_record_that_only_an_admin_can_activate()
    {
        var (accessToken, _, _) = await SetUpAccountAsync("upgrade-flow", ["A", "B"]);

        var upgradeResult = await PostAsync<UpgradeResultDto>(accessToken, "/api/v1/subscription/upgrade", new UpgradeSubscriptionRequest("Pro", "monthly"));
        upgradeResult.Status.Should().Be("Pending");
        upgradeResult.Message.Should().Be("An admin will activate your upgrade.");

        var history = await GetAsync<List<SubscriptionRecordDto>>("/api/v1/subscription/history", accessToken);
        history.Should().ContainSingle(h => h.Reference == upgradeResult.Reference && h.Status == "Pending");

        // A non-admin cannot activate their own (or anyone else's) subscription.
        var selfActivateResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/admin/subscriptions/{upgradeResult.Reference}/activate", accessToken, new ActivateSubscriptionRequest(null));
        selfActivateResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var adminToken = await AdminLoginAsync();
        var activateResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/admin/subscriptions/{upgradeResult.Reference}/activate", adminToken, new ActivateSubscriptionRequest(null));
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var status = await GetAsync<SubscriptionStatusDto>("/api/v1/subscription/status", accessToken);
        status.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Expiring_trials_and_subscriptions_expires_a_stale_active_subscription_past_its_period_end()
    {
        var (accessToken, _, userId) = await SetUpAccountAsync("expire-subscription", ["A", "B"]);
        var reference = await UpgradeAndActivateAsync(accessToken, periodEnd: DateTimeOffset.UtcNow.AddDays(-1));

        // The trial itself also has to be over, otherwise the account would still
        // read as Pro from the trial alone regardless of what happens to the
        // subscription record, this test is specifically about the subscription
        // side of expiry.
        await ExpireTrialAsync(userId);

        using var scope = _factory.Services.CreateScope();
        var maintenanceJobs = scope.ServiceProvider.GetRequiredService<IMaintenanceJobs>();
        await maintenanceJobs.ExpireTrialsAndSubscriptionsAsync();

        var status = await GetAsync<SubscriptionStatusDto>("/api/v1/subscription/status", accessToken);
        status.Status.Should().Be("None");
        status.Plan.Should().Be("Free");
    }

    [Fact]
    public async Task Expiring_trials_does_not_pause_recurring_items_for_someone_who_already_has_an_active_pro_subscription()
    {
        var (accessToken, categories, userId) = await SetUpAccountAsync("expire-keeps-pro-active", ["A", "B"]);
        await UpgradeAndActivateAsync(accessToken);

        var item = await PostAsync<RecurringItemDto>(accessToken, "/api/v1/recurring", new CreateRecurringItemRequest(
            "Expense", 50m, "Netflix", categories.First(c => c.Name == "A").Id, null, "Monthly", 1, "Remind"));

        await ExpireTrialAsync(userId);

        using (var scope = _factory.Services.CreateScope())
        {
            var maintenanceJobs = scope.ServiceProvider.GetRequiredService<IMaintenanceJobs>();
            await maintenanceJobs.ExpireTrialsAndSubscriptionsAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
            var stillThere = await dbContext.RecurringItems.FirstAsync(r => r.Id == item.Id);
            stillThere.IsActive.Should().BeTrue();
        }
    }

    private async Task<string> UpgradeAndActivateAsync(string accessToken, DateTimeOffset? periodEnd = null)
    {
        var upgradeResult = await PostAsync<UpgradeResultDto>(accessToken, "/api/v1/subscription/upgrade", new UpgradeSubscriptionRequest("Pro", "monthly"));
        var adminToken = await AdminLoginAsync();
        var activateResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/admin/subscriptions/{upgradeResult.Reference}/activate", adminToken, new ActivateSubscriptionRequest(periodEnd));
        activateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        return upgradeResult.Reference;
    }

    private async Task<string> AdminLoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin@natoshare.test", "NatoshareAdmin1"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var auth = (await response.Content.ReadFromJsonAsync<AuthResult>())!;
        return auth.AccessToken;
    }

    private async Task ExpireTrialAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        var user = await dbContext.Users.FirstAsync(u => u.Id == userId);
        user.TrialEndsAt = DateTimeOffset.UtcNow.AddDays(-1);
        await dbContext.SaveChangesAsync();
    }

    private async Task<(string AccessToken, List<CategoryDto> Categories, Guid UserId)> SetUpAccountAsync(string emailPrefix, string[] categoryNames)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Gating Tester"));
        signupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = (await signupResponse.Content.ReadFromJsonAsync<AuthResult>())!;

        var now = DateTime.UtcNow;
        var percentage = 100m / categoryNames.Length;
        var categoryInputs = categoryNames
            .Select((name, index) => new OnboardingCategoryInput(name, "Standard", index == categoryNames.Length - 1 ? 100m - (percentage * (categoryNames.Length - 1)) : percentage, null, null))
            .ToList();

        // Effective a few months back (not just "now") so a test can safely backdate
        // a transaction into an earlier month without hitting "no allocation
        // version covers that month yet".
        var effectiveFrom = new DateTime(now.Year, now.Month, 1).AddMonths(-4);
        var onboardingRequest = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"), "UTC", "en-US", 1000m, new MonthInput(effectiveFrom.Year, effectiveFrom.Month), categoryInputs);

        var onboardingResponse = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", auth.AccessToken, onboardingRequest);
        onboardingResponse.StatusCode.Should().Be(HttpStatusCode.OK, await onboardingResponse.Content.ReadAsStringAsync());

        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", auth.AccessToken);

        return (auth.AccessToken, categories, auth.User.Id);
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
