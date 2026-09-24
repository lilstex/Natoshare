using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Application.Admin;
using Natoshare.Application.Auth;
using Natoshare.Infrastructure.Persistence;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Walks the Phase 10 admin app against a real running API and a real Postgres
// database, matching this phase's own Verify criteria: every admin action is
// recorded with a before/after, every /admin/* route refuses a non-admin, and
// destructive actions need a typed confirmation the backend actually checks, not
// just the frontend (docs/04-admin-app.md).
public class AdminAppTests : IClassFixture<NatoshareApiFactory>
{
    private readonly NatoshareApiFactory _factory;
    private readonly HttpClient _client;

    public AdminAppTests(NatoshareApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_normal_user_gets_forbidden_on_every_admin_route()
    {
        var (accessToken, _) = await SetUpAccountAsync("non-admin");

        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/users", accessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/audit", accessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/metrics", accessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/plans", accessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/feature-flags", accessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/settings", accessToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Suspending_a_user_records_a_before_and_after_and_blocks_their_next_login()
    {
        var (_, email) = await SetUpAccountAsync("suspend-me");
        var adminToken = await AdminLoginAsync();

        var userId = await GetUserIdByEmailAsync(email);

        var suspendResponse = await SendWithToken(
            HttpMethod.Post, $"/api/v1/admin/users/{userId}/suspend", adminToken, new SuspendUserRequest("Suspicious activity"));
        suspendResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "correct-horse-1"));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var audit = await GetAsync<AdminAuditSearchResult>($"/api/v1/admin/audit?entityType=User&action=UserSuspended", adminToken);
        var entry = audit.Items.Should().ContainSingle(a => a.EntityId == userId.ToString()).Subject;
        entry.Before.Should().Contain("Active");
        entry.After.Should().Contain("Suspended").And.Contain("Suspicious activity");

        var reactivateResponse = await SendWithToken(HttpMethod.Post, $"/api/v1/admin/users/{userId}/reactivate", adminToken);
        reactivateResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var loginAfterReactivate = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, "correct-horse-1"));
        loginAfterReactivate.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Hard_delete_refuses_the_wrong_confirmation_text_and_deletes_on_the_right_one()
    {
        var (_, email) = await SetUpAccountAsync("delete-me");
        var adminToken = await AdminLoginAsync();
        var userId = await GetUserIdByEmailAsync(email);

        var wrongConfirm = await SendWithToken(
            HttpMethod.Delete, $"/api/v1/admin/users/{userId}", adminToken, new HardDeleteUserRequest("not-the-email"));
        wrongConfirm.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var detailStillThere = await SendWithToken(HttpMethod.Get, $"/api/v1/admin/users/{userId}", adminToken);
        detailStillThere.StatusCode.Should().Be(HttpStatusCode.OK);

        var rightConfirm = await SendWithToken(
            HttpMethod.Delete, $"/api/v1/admin/users/{userId}", adminToken, new HardDeleteUserRequest(email));
        rightConfirm.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var detailAfterDelete = await SendWithToken(HttpMethod.Get, $"/api/v1/admin/users/{userId}", adminToken);
        detailAfterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Adjusting_a_users_plan_to_pro_grants_pro_entitlements_immediately()
    {
        var (accessToken, email) = await SetUpAccountAsync("plan-adjust");
        var adminToken = await AdminLoginAsync();
        var userId = await GetUserIdByEmailAsync(email);

        // Push the trial into the past first, otherwise the trial alone already
        // grants Pro and the assertion below would not prove the plan adjustment did
        // anything (the same pitfall the Phase 9 tests hit for the same reason).
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
            var user = await dbContext.Users.FirstAsync(u => u.Id == userId);
            user.TrialEndsAt = DateTimeOffset.UtcNow.AddDays(-1);
            await dbContext.SaveChangesAsync();
        }

        var adjustResponse = await SendWithToken(
            HttpMethod.Patch, $"/api/v1/admin/users/{userId}/plan", adminToken, new AdjustPlanRequest("Pro", null));
        adjustResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var entitlements = await GetAsync<PlanEntitlementsProbe>("/api/v1/me/entitlements", accessToken);
        entitlements.Plan.Should().Be("Pro");
    }

    [Fact]
    public async Task Feature_flags_plan_configs_and_settings_can_be_read_and_patched_by_an_admin()
    {
        var adminToken = await AdminLoginAsync();

        var flagUpdate = await SendWithToken(
            HttpMethod.Patch, "/api/v1/admin/feature-flags/PaymentsEnabled", adminToken, new PatchFeatureFlagRequest(true));
        flagUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await flagUpdate.Content.ReadFromJsonAsync<AdminFeatureFlagDto>())!.Enabled.Should().BeTrue();

        var planUpdate = await SendWithToken(
            HttpMethod.Patch, "/api/v1/admin/plans/Free", adminToken, new PatchPlanConfigRequest(6, 90, false, false, false, false));
        planUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await planUpdate.Content.ReadFromJsonAsync<AdminPlanConfigDto>())!.MaxCategories.Should().Be(6);

        var settingUpdate = await SendWithToken(
            HttpMethod.Patch, "/api/v1/admin/settings/TrialDays", adminToken, new PatchSettingRequest("14"));
        settingUpdate.StatusCode.Should().Be(HttpStatusCode.OK);
        (await settingUpdate.Content.ReadFromJsonAsync<SystemSettingDto>())!.Value.Should().Be("14");

        // Restore the Free plan config so later tests in this same run (or a shared
        // database in local dev) do not inherit this test's tweaked limits.
        await SendWithToken(HttpMethod.Patch, "/api/v1/admin/plans/Free", adminToken, new PatchPlanConfigRequest(4, 60, false, false, false, false));
        await SendWithToken(HttpMethod.Patch, "/api/v1/admin/settings/TrialDays", adminToken, new PatchSettingRequest("30"));
    }

    [Fact]
    public async Task The_hangfire_dashboard_token_opens_the_dashboard_but_is_useless_against_admin_api_routes()
    {
        // A Phase 11 security-review fix: the admin app's "Open Hangfire dashboard"
        // link must not carry the caller's real, full-privilege session token in a
        // URL (browser history, proxy logs, same-origin Referer headers all a real
        // risk for a token that has to live in a URL). Instead it mints this
        // narrow, short-lived token, which should open the dashboard but fail
        // every other Admin-gated route since it carries no role claim.
        var adminToken = await AdminLoginAsync();

        var tokenResponse = await SendWithToken(HttpMethod.Get, "/api/v1/admin/hangfire-token", adminToken);
        tokenResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboardToken = (await tokenResponse.Content.ReadFromJsonAsync<HangfireTokenResponse>())!.Token;

        var dashboardResponse = await _client.GetAsync($"/hangfire?accessToken={dashboardToken}");
        dashboardResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var usersResponse = await SendWithToken(HttpMethod.Get, "/api/v1/admin/users", dashboardToken);
        usersResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private record HangfireTokenResponse(string Token);

    [Fact]
    public async Task Monitoring_endpoints_respond_for_an_admin()
    {
        var adminToken = await AdminLoginAsync();

        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/metrics", adminToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/health", adminToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/jobs", adminToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/integrity-check", adminToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await SendWithToken(HttpMethod.Get, "/api/v1/admin/errors", adminToken)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private record PlanEntitlementsProbe(string Plan);

    private async Task<Guid> GetUserIdByEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        return (await dbContext.Users.FirstAsync(u => u.Email == email)).Id;
    }

    private async Task<string> AdminLoginAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest("admin@natoshare.test", "NatoshareAdmin1"));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var auth = (await response.Content.ReadFromJsonAsync<AuthResult>())!;
        return auth.AccessToken;
    }

    private async Task<(string AccessToken, string Email)> SetUpAccountAsync(string emailPrefix)
    {
        var email = $"{emailPrefix}+{Guid.NewGuid():N}@example.com";
        var signupResponse = await _client.PostAsJsonAsync("/api/v1/auth/signup", new SignupRequest(email, "correct-horse-1", "Admin Test User"));
        signupResponse.StatusCode.Should().Be(HttpStatusCode.OK, await signupResponse.Content.ReadAsStringAsync());
        var auth = (await signupResponse.Content.ReadFromJsonAsync<AuthResult>())!;
        return (auth.AccessToken, email);
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
