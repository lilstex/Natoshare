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

                // A browser download (window.open) cannot send an Authorization
                // header, so these routes also accept the token as a query string
                // param. Scoped narrowly to just these two routes, not every
                // request, so a token never needs to show up in a URL anywhere else.
                // /hangfire needs this too since the admin app just opens it in a new
                // tab as a plain link, it cannot attach a bearer header to that
                // (docs/04-admin-app.md section 2.5, "gated to admins").
                bearerOptions.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["accessToken"];
                        var isTokenViaQueryAllowed = context.Request.Path.StartsWithSegments("/api/v1/reports/export")
                            || context.Request.Path.StartsWithSegments("/hangfire");

                        if (!string.IsNullOrEmpty(accessToken) && isTokenViaQueryAllowed)
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("RequireUser", policy => policy.RequireAuthenticatedUser())
            .AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));

        return services;
    }
}
