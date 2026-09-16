using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Natoshare.Api.Auth;
using Natoshare.Api.Middleware;
using Natoshare.Application.Auth;
using Natoshare.Application.Common;
using Natoshare.Infrastructure;
using Natoshare.Infrastructure.Persistence;
using Natoshare.Infrastructure.Seed;
using Serilog;

// This is the entry point of the Natoshare API. It sets up everything the app needs
// before it starts listening for requests: logging, the database, health checks,
// Swagger docs, CORS, and rate limiting. Real feature endpoints get added in later
// phases, this phase is only about getting the app to boot up correctly.

// We set up Serilog first, before the host is even built, so that if something goes
// wrong very early during startup, we still get a proper log about it instead of a
// silent crash.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.FromLogContext()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Tell the app to use Serilog for all its logging, and read extra Serilog settings
    // from appsettings.json if we add any later.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console());

    // Controllers are how we will expose the API endpoints (income, expenses, and so on
    // in later phases).
    builder.Services.AddControllers();

    // Swagger reads our controllers and builds an API doc page at /swagger.
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "Natoshare API",
            Version = "v1",
            Description = "The personal budgeting and accounting API behind Natoshare."
        });
    });

    // This makes error responses follow a standard shape (RFC 7807 problem details)
    // instead of every part of the app inventing its own error format.
    builder.Services.AddProblemDetails();

    // CORS controls which websites are allowed to call this API from a browser.
    // The allowed origins come from config so we can set them differently per
    // environment without changing code.
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            }
        });
    });

    // A simple starting point for rate limiting. This gives every client a fair window
    // of requests. We will add stricter rules for endpoints like /auth/* in a later
    // phase when those endpoints exist.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });

    // This wires up the Postgres database connection and the login system (see
    // Natoshare.Infrastructure).
    builder.Services.AddNatoshareInfrastructure(builder.Configuration);

    // Checks how an access token is signed and who is allowed to call what.
    builder.Services.AddNatoshareAuthentication(builder.Configuration, builder.Environment);

    // Lets services read who is calling right now, from their access token.
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

    // Finds every FluentValidation validator in the Application project (SignupRequestValidator
    // and so on) and makes them available to inject, instead of registering each one by hand.
    builder.Services.AddValidatorsFromAssemblyContaining<SignupRequestValidator>();

    // Health checks let us (and our deploy tooling) ask "is this app actually working?"
    // We check the database connection here too, not just that the process is running.
    // The connection string is read from IConfiguration lazily (through the service
    // provider), for the same reason the DbContext reads it lazily, see
    // Natoshare.Infrastructure/InfrastructureServiceCollectionExtensions.cs.
    builder.Services.AddHealthChecks()
        .AddNpgSql(sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("App")!, name: "postgres");

    var app = builder.Build();

    // This has to sit before everything else, so it can catch errors thrown by
    // anything further down the pipeline, including controllers.
    app.UseMiddleware<DomainExceptionMiddleware>();

    // In development we show full Swagger docs. In production this can be locked down
    // to admins only, we will revisit that in the hardening phase.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseCors("Default");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

    // Only in development: bring the database schema up to date, then make sure the
    // User/Admin roles and a super-admin login exist. The migration has to happen
    // first, seeding roles into a database with no tables yet would fail.
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        await dbContext.Database.MigrateAsync();

        var adminEmail = app.Configuration["Seed:AdminEmail"];
        var adminPassword = app.Configuration["Seed:AdminPassword"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            await DevDataSeeder.SeedAsync(scope.ServiceProvider, adminEmail, adminPassword);
        }
    }

    // The budget templates (Everyday, 50/30/20, and so on) are real content the
    // onboarding wizard needs, in every environment, not just development. If the
    // database has not been migrated yet, this just logs a warning instead of
    // crashing the whole app on startup.
    try
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        await Natoshare.Infrastructure.Seed.ReferenceDataSeeder.SeedAsync(dbContext);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Could not seed budget templates, has the database been migrated yet?");
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // If startup fails for any reason, we want that written to the log clearly instead
    // of just crashing quietly.
    Log.Fatal(ex, "Natoshare API failed to start");
}
finally
{
    Log.CloseAndFlush();
}
