using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Natoshare.Application.Auth;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;
using Natoshare.Application.Me;
using Natoshare.Domain.Identity;
using Natoshare.Infrastructure.Audit;
using Natoshare.Infrastructure.Budgeting;
using Natoshare.Infrastructure.Identity;
using Natoshare.Infrastructure.Persistence;
using Natoshare.Infrastructure.Time;

namespace Natoshare.Infrastructure;

// This class is how the Api project sets up everything from Infrastructure: the
// database, the login system, and the services that use them. The Api project just
// calls one method here, it does not need to know EF Core or ASP.NET Identity are
// even involved.
public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddNatoshareInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // We read the connection string lazily, from IConfiguration inside the options
        // callback, instead of reading it once right here. If we read it here, tests
        // that swap in a different database after this method runs (like our
        // integration tests do, pointing at a throwaway Postgres) would not see it,
        // this one line would already have "locked in" the wrong connection string.
        services.AddDbContext<NatoshareDbContext>((serviceProvider, options) =>
        {
            var liveConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = liveConfiguration.GetConnectionString("App")
                ?? throw new InvalidOperationException(
                    "The 'App' connection string is missing. Check appsettings or your environment variables.");

            options.UseNpgsql(connectionString);
        });

        // Settings that come from config, bound once here so services can just ask
        // for IOptions<T> instead of reading IConfiguration themselves.
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.Configure<AppDefaults>(configuration.GetSection("App"));

        // AddIdentityCore, not AddIdentity, because this is an API with no cookie
        // login or Razor pages, we only need the user/role management pieces.
        services.AddIdentityCore<User>(options =>
        {
            options.User.RequireUniqueEmail = true;

            // Our own SignupRequestValidator already checks length. We keep Identity's
            // rules simple and matching, so the two never disagree with each other.
            options.Password.RequiredLength = 8;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<NatoshareDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IClock, SystemClock>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ICurrencyCatalog, CurrencyCatalog>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMeService, MeService>();

        // AllocationService is registered as itself (not just as IAllocationService),
        // because CategoryService and OnboardingService both reuse a couple of its
        // internal helpers directly, so all three need to share the very same
        // instance within one request.
        services.AddScoped<AllocationService>();
        services.AddScoped<IAllocationService>(sp => sp.GetRequiredService<AllocationService>());
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IOnboardingService, OnboardingService>();

        return services;
    }
}
