using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
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

    // This has to sit before everything else, so it can catch errors thrown by
    // anything further down the pipeline, including controllers.
    app.UseMiddleware<DomainExceptionMiddleware>();

    // In development we show full Swagger docs. In production this can be locked down
    // to admins only, we will revisit that in the hardening phase.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();

        // Same story as Swagger, wide open in development only, admin-only access
        // is hardening-phase work.
        app.UseHangfireDashboard();
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
