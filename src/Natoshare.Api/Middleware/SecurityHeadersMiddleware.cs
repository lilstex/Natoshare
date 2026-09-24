namespace Natoshare.Api.Middleware;

// A few response headers every browser respects, that cost nothing to add and close
// off a handful of classic attacks: a page cannot be framed (clickjacking), a
// response cannot be sniffed into a different content type than we said it was, and
// once a visitor has been here over HTTPS, their browser remembers to never try
// plain HTTP again. There is deliberately no Content-Security-Policy header here,
// Swagger's and Hangfire's own dashboards both rely on inline scripts/styles that a
// safe CSP would need real per-page tuning to allow, left as documented future work
// rather than shipping a policy nobody checked.
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            if (context.Request.IsHttps)
            {
                headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
