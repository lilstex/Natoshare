using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure;

// This class is how the Api project sets up everything from Infrastructure, like the
// database. The Api project just calls one method here, it does not need to know that
// we are using EF Core or Postgres under the hood.
public static class InfrastructureServiceCollectionExtensions
{
    // Wires up the Postgres database connection using the "App" connection string.
    public static IServiceCollection AddNatoshareInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("App")
            ?? throw new InvalidOperationException(
                "The 'App' connection string is missing. Check appsettings or your environment variables.");

        services.AddDbContext<NatoshareDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
