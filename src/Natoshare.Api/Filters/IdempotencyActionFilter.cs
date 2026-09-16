using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Natoshare.Application.Common;
using Natoshare.Domain.Common;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Api.Filters;

// Makes a write safe to retry. If a caller sends the same Idempotency-Key header
// twice (a slow network making someone tap "save" again), the second request just
// gets back the first result instead of logging the same income or expense twice.
// This only does anything when the header is actually sent, a normal request without
// it behaves exactly as before.
public class IdempotencyActionFilter : IAsyncActionFilter
{
    private const string HeaderName = "Idempotency-Key";

    private readonly NatoshareDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly JsonSerializerOptions _jsonOptions;

    public IdempotencyActionFilter(NatoshareDbContext dbContext, ICurrentUser currentUser, IClock clock, IOptions<JsonOptions> jsonOptions)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;

        // Has to match the JSON casing ASP.NET Core actually uses for a normal
        // response (camelCase), otherwise a replayed result would come back looking
        // different from the original one, PascalCase instead of camelCase.
        _jsonOptions = jsonOptions.Value.JsonSerializerOptions;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            await next();
            return;
        }

        var key = headerValues.ToString();
        if (string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        var userId = _currentUser.UserId;
        var cancellationToken = context.HttpContext.RequestAborted;

        var existing = await _dbContext.IdempotencyRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == userId && r.Key == key, cancellationToken);

        if (existing is not null)
        {
            using var cachedDocument = JsonDocument.Parse(existing.ResponseBody);
            context.Result = new ObjectResult(cachedDocument.RootElement.Clone()) { StatusCode = existing.ResponseStatusCode };
            return;
        }

        var executed = await next();

        // Something went wrong, or the action itself did not produce a plain object
        // result, there is nothing safe to remember here.
        if (executed.Exception is not null || executed.Result is not ObjectResult { StatusCode: null or >= 200 and < 300 } objectResult)
        {
            return;
        }

        var record = new IdempotencyRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Key = key,
            ResponseStatusCode = objectResult.StatusCode ?? 200,
            ResponseBody = JsonSerializer.Serialize(objectResult.Value, _jsonOptions),
            CreatedAt = _clock.UtcNow,
        };

        _dbContext.IdempotencyRecords.Add(record);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Someone else's request with the same key won the race to save first,
            // their result is the one that counts, this is not a real error.
        }
    }
}
