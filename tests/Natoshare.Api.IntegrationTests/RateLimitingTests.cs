using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Natoshare.Application.Auth;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// A dedicated, much lower auth rate limit than every other test in this project
// uses (see NatoshareApiFactory, which raises the limit out of the way for
// everyone else since they legitimately sign up or log in far more than a real
// attacker's limit allows within a minute). This is the one place that actually
// checks the limit does something, added in Phase 11's hardening pass.
public class RateLimitedApiFactory : NatoshareApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:AuthPermitLimit"] = "3",
            });
        });
    }
}

public class RateLimitingTests : IClassFixture<RateLimitedApiFactory>
{
    private readonly HttpClient _client;

    public RateLimitingTests(RateLimitedApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_burst_of_login_attempts_past_the_auth_limit_gets_rate_limited()
    {
        var request = new LoginRequest("nobody@example.com", "wrong-password");

        for (var i = 0; i < 3; i++)
        {
            var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request);
            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var oneOverTheLimit = await _client.PostAsJsonAsync("/api/v1/auth/login", request);
        oneOverTheLimit.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
