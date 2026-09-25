using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Ledger;
using Natoshare.Application.Maintenance;
using Natoshare.Application.Me;
using Natoshare.Application.Months;
using Natoshare.Application.Notifications;
using Natoshare.Application.Planning;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Infrastructure.Persistence;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the Phase 7 recurring-items feature and the three housekeeping jobs it
// finally finishes (ExpireTrialsAndSubscriptions, PurgePendingDeletions,
// LedgerIntegrityCheck), against a real running API and a real Postgres database.
public class RecurringItemsAndMaintenanceTests : IClassFixture<NatoshareApiFactory>
{
    private readonly NatoshareApiFactory _factory;
    private readonly HttpClient _client;

    public RecurringItemsAndMaintenanceTests(NatoshareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_monthly_autopost_expense_posts_a_real_expense_and_advances_to_next_month()
    {
        var (accessToken, feeding, _) = await SetUpAccountAsync("autopost-expense", 1000m);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var item = await PostAsync<RecurringItemDto>(accessToken, "/api/v1/recurring", new CreateRecurringItemRequest(
            "Expense", 50m, "Netflix", feeding.Id, null, "Monthly", today.Day <= 28 ? today.Day : 28, "AutoPost"));

        await MaterializeAsync();

        var expenses = await GetAsync<List<ExpenseDto>>("/api/v1/expenses", accessToken);
        expenses.Should().Contain(e => e.Description == "Netflix" && e.Amount == 50m);

        var afterwards = await GetAsync<List<RecurringItemDto>>("/api/v1/recurring", accessToken);
        var updated = afterwards.First(r => r.Id == item.Id);
        updated.LastPostedOn.Should().Be(item.NextRunOn);
        updated.NextRunOn.Should().BeAfter(item.NextRunOn);
        updated.NextRunOn.Month.Should().Be(item.NextRunOn.Month == 12 ? 1 : item.NextRunOn.Month + 1);
    }

    [Fact]
    public async Task A_weekly_remind_item_creates_a_notification_and_never_posts_anything()
    {
        var (accessToken, _, _) = await SetUpAccountAsync("remind-income", 1000m);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var item = await PostAsync<RecurringItemDto>(accessToken, "/api/v1/recurring", new CreateRecurringItemRequest(
            "Income", 15000m, "Freelance gig", null, "Flexible", "Weekly", (int)today.DayOfWeek, "Remind"));

        await MaterializeAsync();

        var notifications = await GetAsync<List<NotificationDto>>("/api/v1/notifications?kind=RecurringItemDue", accessToken);
        notifications.Should().Contain(n => n.RelatedEntityId == item.Id.ToString());

        var incomes = await GetAsync<List<IncomeDto>>("/api/v1/income", accessToken);
        incomes.Should().NotContain(i => i.Description == "Freelance gig");

        var afterwards = await GetAsync<List<RecurringItemDto>>("/api/v1/recurring", accessToken);
        var updated = afterwards.First(r => r.Id == item.Id);
        updated.LastPostedOn.Should().BeNull();
        updated.NextRunOn.Should().Be(item.NextRunOn.AddDays(7));
    }

    [Fact]
    public async Task Skip_next_advances_the_schedule_without_posting_or_reminding_for_the_skipped_occurrence()
    {
        var (accessToken, feeding, _) = await SetUpAccountAsync("skip-next", 1000m);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var item = await PostAsync<RecurringItemDto>(accessToken, "/api/v1/recurring", new CreateRecurringItemRequest(
            "Expense", 50m, "Gym", feeding.Id, null, "Monthly", today.Day <= 28 ? today.Day : 28, "AutoPost"));

        var afterSkip = await PostAsync<RecurringItemDto>(accessToken, $"/api/v1/recurring/{item.Id}/skip-next", new { });
        afterSkip.NextRunOn.Should().BeAfter(item.NextRunOn);

        await MaterializeAsync();

        var expenses = await GetAsync<List<ExpenseDto>>("/api/v1/expenses", accessToken);
        expenses.Should().NotContain(e => e.Description == "Gym");
    }

    [Fact]
    public async Task Committed_total_normalises_every_cadence_onto_the_same_monthly_footing()
    {
        var (accessToken, feeding, _) = await SetUpAccountAsync("committed-total", 1000m);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await PostAsync<RecurringItemDto>(accessToken, "/api/v1/recurring", new CreateRecurringItemRequest(
            "Expense", 100m, "Rent share", feeding.Id, null, "Monthly", 1, "Remind"));
        await PostAsync<RecurringItemDto>(accessToken, "/api/v1/recurring", new CreateRecurringItemRequest(
            "Expense", 20m, "Coffee subscription", feeding.Id, null, "Weekly", (int)today.DayOfWeek, "Remind"));

        var committed = await GetAsync<CommittedTotalDto>("/api/v1/recurring/committed-total", accessToken);

        committed.MonthlyExpenseTotal.Should().BeApproximately(100m + (20m * 52m / 12m), 0.01m);
        committed.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_lapsed_trial_is_blocked_from_recurring_items_with_an_upgrade_required_response()
    {
        var (accessToken, _, userId) = await SetUpAccountAsync("lapsed-trial", 1000m);
        await ExpireTrialAsync(userId);

        var response = await SendWithToken(HttpMethod.Post, "/api/v1/recurring", accessToken, new CreateRecurringItemRequest(
            "Expense", 50m, "Netflix", null, null, "Monthly", 1, "Remind"));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("upgrade-required");
    }

    [Fact]
    public async Task Expiring_trials_pauses_active_recurring_items_it_does_not_delete_them()
    {
        var (accessToken, feeding, userId) = await SetUpAccountAsync("expire-pauses", 1000m);
        var item = await PostAsync<RecurringItemDto>(accessToken, "/api/v1/recurring", new CreateRecurringItemRequest(
            "Expense", 50m, "Netflix", feeding.Id, null, "Monthly", 1, "AutoPost"));
        item.IsActive.Should().BeTrue();

        await ExpireTrialAsync(userId);

        using var scope = _factory.Services.CreateScope();
        var maintenanceJobs = scope.ServiceProvider.GetRequiredService<IMaintenanceJobs>();
        await maintenanceJobs.ExpireTrialsAndSubscriptionsAsync();

        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        var stillThere = await dbContext.RecurringItems.FirstAsync(r => r.Id == item.Id);
        stillThere.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Purging_a_pending_deletion_past_its_grace_period_removes_the_account_and_its_data()
    {
        var (accessToken, _, userId, email) = await SetUpAccountWithEmailAsync("purge-me", 1000m);

        var deleteResponse = await SendWithToken(
            HttpMethod.Delete, "/api/v1/me", accessToken, new DeleteAccountRequest("correct-horse-1", "DELETE"));
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Accepted);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
            var user = await dbContext.Users.FirstAsync(u => u.Id == userId);
            user.PendingDeletionRequestedAt = DateTimeOffset.UtcNow.AddDays(-100);
            await dbContext.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var maintenanceJobs = scope.ServiceProvider.GetRequiredService<IMaintenanceJobs>();
            await maintenanceJobs.PurgePendingDeletionsAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
            (await dbContext.Users.AnyAsync(u => u.Id == userId)).Should().BeFalse();
            (await dbContext.Categories.AnyAsync(c => c.UserId == userId)).Should().BeFalse();
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "correct-horse-1" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ledger_integrity_check_finds_nothing_wrong_normally_but_catches_a_deliberately_injected_mismatch()
    {
        var (accessToken, feeding, userId) = await SetUpAccountAsync("integrity-check", 1000m);
        await PostAsync<LogExpenseResult>(
            accessToken, "/api/v1/expenses", new LogExpenseRequest(300m, "Groceries", "Category", feeding.Id, null, null, null));

        var now = DateTime.UtcNow;
        var closeResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/months/{now.Year}/{now.Month}/close", accessToken, new CloseMonthRequest(null, [], null, null));
        closeResponse.StatusCode.Should().Be(HttpStatusCode.OK, await closeResponse.Content.ReadAsStringAsync());

        using (var scope = _factory.Services.CreateScope())
        {
            var maintenanceJobs = scope.ServiceProvider.GetRequiredService<IMaintenanceJobs>();
            var cleanDrift = await maintenanceJobs.RunLedgerIntegrityCheckAsync();
            cleanDrift.Should().Be(0);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
            dbContext.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Account = AccountKind.Category,
                AccountCategoryId = feeding.Id,
                EntryType = LedgerEntryType.Adjustment,
                Amount = new Money(1m),
                Direction = LedgerDirection.Credit,
                SourceTxnType = SourceTxnType.AdminAdjustment,
                SourceTxnId = Guid.CreateVersion7(),
                Note = "Deliberately injected drift for a test",
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await dbContext.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var maintenanceJobs = scope.ServiceProvider.GetRequiredService<IMaintenanceJobs>();
            var driftAfterInjection = await maintenanceJobs.RunLedgerIntegrityCheckAsync();
            driftAfterInjection.Should().BeGreaterThanOrEqualTo(1);
        }
    }

    private async Task MaterializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var materializer = scope.ServiceProvider.GetRequiredService<IRecurringItemMaterializer>();
        await materializer.MaterializeDueItemsAsync();
    }

    private async Task ExpireTrialAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        var user = await dbContext.Users.FirstAsync(u => u.Id == userId);
        user.TrialEndsAt = DateTimeOffset.UtcNow.AddDays(-1);
        await dbContext.SaveChangesAsync();
    }

    private async Task<(string AccessToken, CategoryDto Feeding, Guid UserId)> SetUpAccountAsync(string emailPrefix, decimal income)
    {
        var (accessToken, feeding, userId, _) = await SetUpAccountWithEmailAsync(emailPrefix, income);
        return (accessToken, feeding, userId);
    }

    private async Task<(string AccessToken, CategoryDto Feeding, Guid UserId, string Email)> SetUpAccountWithEmailAsync(string emailPrefix, decimal income)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Recurring Tester"));
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
                new OnboardingCategoryInput("Rent", "FixedAccount", 50m, null, null),
            ]);

        var onboardingResponse = await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", auth.AccessToken, onboardingRequest);
        onboardingResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", auth.AccessToken);
        var feeding = categories.First(c => c.Name == "Feeding");

        await PostAsync<IncomeDto>(auth.AccessToken, "/api/v1/income", new LogIncomeRequest("Allocatable", income, "Salary", null));

        return (auth.AccessToken, feeding, auth.User.Id, email);
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
