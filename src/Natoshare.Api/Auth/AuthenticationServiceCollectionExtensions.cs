using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Natoshare.Application.Common;

namespace Natoshare.Api.Auth;

// Sets up how the API checks an access token on every request, and the two policies
// controllers use: RequireUser (any logged in person) and RequireAdmin (Admin role
// only).
public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddNatoshareAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        // A short signing key is easy to guess, so we fail fast on startup unless we
        // are in development, where a short placeholder key is fine. This check runs
        // once here so a bad key is caught immediately instead of on the first login.
        var signingKeyLength = configuration.GetSection("Jwt")["SigningKey"]?.Length ?? 0;
        if (!environment.IsDevelopment() && signingKeyLength < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 characters outside development.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // We build the real token validation rules here, using JwtSettings resolved
        // through the options system instead of reading config directly. This way the
        // values are only read once the app is fully started, which matters for
        // integration tests that swap in test-only settings after this method runs.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearerOptions, jwtSettingsOptions) =>
            {
                var jwtSettings = jwtSettingsOptions.Value;

                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireUser", policy => policy.RequireAuthenticatedUser())
            .AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));

        return services;
    }
}
