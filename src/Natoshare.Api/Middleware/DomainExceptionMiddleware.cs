using Natoshare.Domain.Common;

namespace Natoshare.Api.Middleware;

// This catches the errors we throw on purpose, like "email already used" or "wrong
// password", and turns them into a proper JSON error response with the right status
// code. Anything we did NOT expect just keeps going as a normal unhandled exception,
// which ASP.NET Core's own problem details middleware deals with.
public class DomainExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<DomainExceptionMiddleware> _logger;

    public DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationFailedException ex)
        {
            _logger.LogInformation("Request failed validation: {Errors}", ex.Errors);
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://natoshare.example.com/errors/validation-failed",
                title = ex.Message,
                status = ex.StatusCode,
                errors = ex.Errors,
            });
        }
        catch (DomainException ex)
        {
            _logger.LogInformation(ex, "Request failed a domain rule");
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new
            {
                type = $"https://natoshare.example.com/errors/{ex.GetType().Name}",
                title = ex.Message,
                status = ex.StatusCode,
            });
        }
    }
}
