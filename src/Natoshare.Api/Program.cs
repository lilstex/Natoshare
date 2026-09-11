using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Natoshare.Infrastructure;
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

    // This wires up the Postgres database connection (see Natoshare.Infrastructure).
    builder.Services.AddNatoshareInfrastructure(builder.Configuration);

    // Health checks let us (and our deploy tooling) ask "is this app actually working?"
    // We check the database connection here too, not just that the process is running.
    var connectionString = builder.Configuration.GetConnectionString("App");
    var healthChecksBuilder = builder.Services.AddHealthChecks();
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        healthChecksBuilder.AddNpgSql(connectionString, name: "postgres");
    }

    var app = builder.Build();

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
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health");

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
