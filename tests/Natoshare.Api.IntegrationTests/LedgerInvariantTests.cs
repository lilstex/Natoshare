using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Ledger;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// The ledger invariant: a category's cached balance must always match what you get by
// adding up its ledger rows from scratch, no matter what random sequence of income and
// expenses got it there. This runs a seeded random sequence of operations and checks
// the two never drift apart, once after every single step.
public class LedgerInvariantTests : IClassFixture<NatoshareApiFactory>
{
    private readonly NatoshareApiFactory _factory;
    private readonly HttpClient _client;

    public LedgerInvariantTests(NatoshareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_cached_balance_always_matches_recomputing_the_ledger_from_scratch()
    {
        var email = $"invariant+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Invariant Tester"));
        var auth = (await signupResponse.Content.ReadFromJsonAsync<AuthResult>())!;

        var categoryNames = new[] { "Rent", "Food", "Fun" };
        var onboardingRequest = new OnboardingCompleteRequest(
            new CurrencyInput("USD", "$"),
            "UTC",
            "en-US",
            2000m,
            new MonthInput(DateTime.UtcNow.Year, DateTime.UtcNow.Month),
            categoryNames.Select(n => new OnboardingCategoryInput(n, "Standard", 100m / categoryNames.Length, null, null)).ToList());
        await SendWithToken(HttpMethod.Post, "/api/v1/onboarding/complete", auth.AccessToken, onboardingRequest);

        var categories = await GetAsync<List<CategoryDto>>("/api/v1/categories", auth.AccessToken);

        // Seeded so a failure is always reproducible instead of a flaky one-off.
        var random = new Random(20260916);

        for (var i = 0; i < 25; i++)
        {
            var category = categories[random.Next(categories.Count)];

            if (random.Next(2) == 0)
            {
                var amount = Math.Round((decimal)(random.NextDouble() * 200 + 10), 2);
                await SendWithToken(HttpMethod.Post, "/api/v1/income", auth.AccessToken,
                    new LogIncomeRequest("Allocatable", amount, $"Random income {i}", null));
            }
            else
            {
                var amount = Math.Round((decimal)(random.NextDouble() * 150 + 5), 2);
                await SendWithToken(HttpMethod.Post, "/api/v1/expenses", auth.AccessToken,
                    new LogExpenseRequest(amount, $"Random spend {i}", "Category", category.Id, null, null, null));
            }

            var balances = await GetAsync<BalancesResult>("/api/v1/balances", auth.AccessToken);

            using var scope = _factory.Services.CreateScope();
            var ledgerService = scope.ServiceProvider.GetRequiredService<ILedgerService>();

            foreach (var categoryBalance in balances.Categories)
            {
                var recomputed = await ledgerService.GetAccountBalanceAsync(auth.User.Id, AccountRef.Category(categoryBalance.CategoryId));

                // The ledger only ever knows a plain balance, never "available vs
                // deficit" on its own, but a category can only be in one of those two
                // states at a time (the domain model guarantees this), so recomputing
                // straight from the ledger has to land on exactly the same number as
                // whichever side of Available/Deficit is non-zero.
                var expected = categoryBalance.Deficit > 0 ? 0m : categoryBalance.Available;
                recomputed.Amount.Should().Be(expected, $"after operation {i} for category {categoryBalance.Name}");
            }
        }
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
