using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Natoshare.Api.Auth;
using Natoshare.Api.Filters;
using Natoshare.Api.Jobs;
using Natoshare.Api.Middleware;
using Natoshare.Application.Auth;
using Natoshare.Application.Common;
using Natoshare.Application.Maintenance;
using Natoshare.Application.Months;
using Natoshare.Application.Notifications;
using Natoshare.Application.Planning;
using Natoshare.Infrastructure;
using Natoshare.Infrastructure.Admin;
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

// QuestPDF needs a license picked once at startup, Community is free for a company
// under a small revenue threshold, which is exactly this project's situation.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // A plain `dotnet run` (no Docker) picks up the repo-root .env file here (see
    // .env.example), as this app's lowest-priority config source, a fallback only.
    // It is inserted first, so a real environment variable, an appsettings.*.json
    // value, or (for our own tests) WebApplicationFactory's own override all still
    // win over it without any special handling, they get added after this and
    // later sources always beat earlier ones for the same key. docker-compose.yml
    // never needs this, it already passes these in as real container environment
    // variables directly.
    InsertRootDotEnvFallback(builder.Configuration);

    // Tell the app to use Serilog for all its logging, and read extra Serilog settings
    // from appsettings.json if we add any later.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        // Feeds the admin app's "recent errors" screen (docs/04-admin-app.md
        // section 2.5), see InMemoryErrorSink for why this is a small ring buffer
        // instead of a real log aggregator.
        .WriteTo.Sink(new InMemoryErrorSink()));

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

    // A fair window of requests for everyone, plus a much stricter named policy for
    // the handful of anonymous auth endpoints (signup, login, forgot/reset password)
    // that a bot could otherwise hammer to brute-force a password or spam signups.
    // The global limit still applies underneath it too, this one is extra, not
    // instead of. Both limits are read from config (not hardcoded) so our own
    // integration tests, which legitimately sign up dozens of accounts from the
    // same in-process "IP" inside a single test class, can raise them via
    // NatoshareApiFactory instead of a real attacker's limit being loosened.
    //
    // The config is read inside each factory delegate below, not once into a local
    // variable up here, on purpose: builder.Configuration at this exact point in
    // top-level Program.cs does not yet include a WebApplicationFactory test host's
    // own ConfigureAppConfiguration overrides, those only get layered in while the
    // host finishes building. Reading eagerly here silently captured the default
    // limit forever, ignoring any test override, a real bug caught only by
    // RateLimitingTests actually asserting on the limit instead of just building.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimiting:GlobalPermitLimit", 300),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10),
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

    // Lets a POST endpoint accept an Idempotency-Key header so a retried request
    // never logs the same money twice. Applied per-action with [ServiceFilter], not
    // globally, only the writes that actually create something need it.
    builder.Services.AddScoped<IdempotencyActionFilter>();

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

    // Hangfire runs our recurring background job (checking everyone's pacing and
    // deficits, not just right after a write). It keeps its own tables in the same
    // Postgres database. Same lazy connection-string trick as the DbContext above.
    builder.Services.AddHangfire((serviceProvider, config) =>
    {
        var connectionString = serviceProvider.GetRequiredService<IConfiguration>().GetConnectionString("App")!;
        config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString));
    });
    builder.Services.AddHangfireServer();

    // One place every recurring job's failure gets logged loudly, instead of only
    // ever showing up if someone happens to open the Hangfire dashboard.
    GlobalJobFilters.Filters.Add(new HangfireFailureAlertFilter());

    var app = builder.Build();

    // First of all, so every response gets these headers, including error
    // responses further down the pipeline.
    app.UseMiddleware<SecurityHeadersMiddleware>();

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

    // Gated to the Admin role in every environment (see AdminOnlyDashboardAuthFilter
    // and docs/04-admin-app.md section 2.5), this has to sit after
    // UseAuthentication/UseAuthorization above so httpContext.User is populated by
    // the time the filter runs.
    app.UseHangfireDashboard(options: new DashboardOptions
    {
        Authorization = [new AdminOnlyDashboardAuthFilter()],
    });

    app.MapControllers();
    app.MapHealthChecks("/health");

    // Migrations run automatically in development, for a local workflow where you
    // never have to remember a separate step. Outside development they only run if
    // a deploy explicitly opts in with RUN_MIGRATIONS_ON_STARTUP=true, matching
    // docs/03-architecture.md section 6's "migrations run on startup behind a flag,
    // or as a separate job step in CI/CD": both are supported, whichever a real
    // deploy picks stays a config decision, not a hardcoded one. Bootstrapping a
    // local super-admin login only ever makes sense in development, that part
    // never runs anywhere else regardless of this flag.
    var shouldMigrateOnStartup = app.Environment.IsDevelopment() || app.Configuration.GetValue("RUN_MIGRATIONS_ON_STARTUP", false);
    if (shouldMigrateOnStartup)
    {
        using var scope = app.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NatoshareDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    // The budget templates, PlanConfig rows, feature flags and system settings are
    // real content the app needs in every environment, not just development. This
    // has to run before DevDataSeeder below: seeding a demo account means calling
    // straight into CategoryService/EntitlementService, which need a PlanConfig row
    // to already exist. If the database has not been migrated yet, this just logs a
    // warning instead of crashing the whole app on startup.
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

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();

        var adminEmail = app.Configuration["Seed:AdminEmail"];
        var adminPassword = app.Configuration["Seed:AdminPassword"];

        if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
        {
            await DevDataSeeder.SeedAsync(scope.ServiceProvider, adminEmail, adminPassword);
        }
    }

    // Checks every user's pacing and deficits once an hour, this is what catches an
    // alert that time passing causes (like a category quietly drifting over pace)
    // instead of a new write, those are already checked right when they happen.
    RecurringJob.AddOrUpdate<IAlertEvaluationService>(
        "evaluate-alerts",
        service => service.EvaluateAllOpenMonthsAsync(CancellationToken.None),
        Cron.Hourly());

    // Once a day, nudges anyone who still has an old month sitting Open well past
    // when it ended, so a month never just gets forgotten about.
    RecurringJob.AddOrUpdate<IMonthLifecycleService>(
        "month-close-reminders",
        service => service.EvaluateCloseRemindersAsync(CancellationToken.None),
        Cron.Daily());

    // Once a day, checks every debt and loan for a due or expected-return date
    // coming up or already passed, and every open promise the Flexible Pool can now
    // actually afford to redeem.
    RecurringJob.AddOrUpdate<IAlertEvaluationService>(
        "evaluate-obligations",
        service => service.EvaluateObligationsAsync(CancellationToken.None),
        Cron.Daily());

    // Once an hour, posts or reminds for every recurring item that has come due.
    RecurringJob.AddOrUpdate<IRecurringItemMaterializer>(
        "materialise-recurring-items",
        service => service.MaterializeDueItemsAsync(CancellationToken.None),
        Cron.Hourly());

    // Once a day, pauses recurring items for anyone whose trial has lapsed.
    RecurringJob.AddOrUpdate<IMaintenanceJobs>(
        "expire-trials-and-subscriptions",
        service => service.ExpireTrialsAndSubscriptionsAsync(CancellationToken.None),
        Cron.Daily(0, 30));

    // Once a day, hard-deletes any account whose PendingDeletion grace period is over.
    RecurringJob.AddOrUpdate<IMaintenanceJobs>(
        "purge-pending-deletions",
        service => service.PurgePendingDeletionsAsync(CancellationToken.None),
        Cron.Daily(2, 0));

    // Once a night, re-checks that every settled category's ledger account still
    // nets to exactly zero, logging an error (see HangfireFailureAlertFilter and the
    // job's own logging) if anything has drifted.
    RecurringJob.AddOrUpdate<IMaintenanceJobs>(
        "ledger-integrity-check",
        service => service.RunLedgerIntegrityCheckAsync(CancellationToken.None),
        Cron.Daily(3, 0));

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

// Walks up from the current directory looking for a .env file (a plain `dotnet
// run` starts inside src/Natoshare.Api, a few folders below the repo root where
// .env actually lives), reads its flat KEY=VALUE lines, and builds the same
// ConnectionStrings:App / Jwt:SigningKey / Seed:Admin* keys docker-compose.yml's
// own variable substitution builds for the container, from the same DB_HOST,
// JWT_SIGNING_KEY and so on names .env.example documents.
//
// Each one only ever gets added if the real, already-composed configuration does
// not already have a non-empty value for it. This is deliberately a value check,
// not a source-ordering trick: appsettings.json ships each of these as an empty
// string placeholder (`"App": ""`), not an absent key, and an explicit empty
// string from a higher-precedence source still counts as "present" as far as the
// configuration system is concerned, it would silently beat a lower-priority
// source's real value no matter where that source sits in the list. Checking the
// value directly is what actually keeps a real environment variable, a real
// appsettings.*.json value, or (for our own tests) WebApplicationFactory's own
// override safely in charge over this fallback.
static void InsertRootDotEnvFallback(ConfigurationManager configuration)
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, ".env")))
    {
        directory = directory.Parent;
    }

    if (directory is null)
    {
        return;
    }

    var envValues = new Dictionary<string, string>();
    foreach (var line in File.ReadAllLines(Path.Combine(directory.FullName, ".env")))
    {
        var trimmed = line.Trim();
        var separatorIndex = trimmed.IndexOf('=');
        if (trimmed.Length == 0 || trimmed.StartsWith('#') || separatorIndex <= 0)
        {
            continue;
        }

        envValues[trimmed[..separatorIndex].Trim()] = trimmed[(separatorIndex + 1)..].Trim();
    }

    var fallback = new Dictionary<string, string?>();

    void AddIfRealValueMissing(string configKey, string? candidate)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && string.IsNullOrWhiteSpace(configuration[configKey]))
        {
            fallback[configKey] = candidate;
        }
    }

    if (envValues.TryGetValue("DB_HOST", out var host) && !string.IsNullOrWhiteSpace(host))
    {
        var port = envValues.GetValueOrDefault("DB_PORT", "5432");
        var database = envValues.GetValueOrDefault("DB_NAME", "natoshare");
        var user = envValues.GetValueOrDefault("DB_USER", "natoshare");
        var password = envValues.GetValueOrDefault("DB_PASSWORD", "");
        AddIfRealValueMissing("ConnectionStrings:App", $"Host={host};Port={port};Database={database};Username={user};Password={password}");
    }

    AddIfRealValueMissing("Jwt:SigningKey", envValues.GetValueOrDefault("JWT_SIGNING_KEY"));
    AddIfRealValueMissing("Seed:AdminEmail", envValues.GetValueOrDefault("SEED_ADMIN_EMAIL"));
    AddIfRealValueMissing("Seed:AdminPassword", envValues.GetValueOrDefault("SEED_ADMIN_PASSWORD"));

    if (fallback.Count > 0)
    {
        configuration.AddInMemoryCollection(fallback);
    }
}
