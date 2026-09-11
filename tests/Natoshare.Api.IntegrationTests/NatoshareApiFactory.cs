using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Natoshare.Api.IntegrationTests;

// Starts a real, throwaway Postgres in Docker and boots the whole API against it, so
// our tests catch real database problems instead of just working against a fake.
public class NatoshareApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("natoshare_test")
        .WithUsername("natoshare")
        .WithPassword("natoshare_test_password")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development, so /auth/forgot-password hands back the raw reset code, which
        // the tests need since there is no email system to read it from.
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:App"] = _postgres.GetConnectionString(),
                ["Jwt:SigningKey"] = "integration-test-signing-key-that-is-long-enough",
                ["Seed:AdminEmail"] = "admin@natoshare.test",
                ["Seed:AdminPassword"] = "NatoshareAdmin1",
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // The tables need to exist before any test runs.
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    // xUnit calls this one (it wants a Task), the base class's own IAsyncDisposable
    // still runs separately to clean up the test server itself.
    Task IAsyncLifetime.DisposeAsync() => _postgres.StopAsync();
}
