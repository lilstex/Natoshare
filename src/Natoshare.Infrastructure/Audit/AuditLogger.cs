using System.Text.Json;
using Natoshare.Application.Common;
using Natoshare.Domain.Audit;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Audit;

// Writes one row to the audit log every time we call it. For now we call this
// directly from the auth code for things like logins and password changes. Once we
// have real entities worth tracking (categories, income, and so on) we can add an EF
// Core interceptor on top of this that catches changes automatically.
public class AuditLogger : IAuditLogger
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;

    public AuditLogger(NatoshareDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task LogAsync(
        Guid? actorUserId,
        string actorRole,
        string action,
        string entityType,
        string entityId,
        string? ip,
        string? userAgent,
        string? before = null,
        string? after = null,
        CancellationToken cancellationToken = default)
    {
        _dbContext.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            ActorUserId = actorUserId,
            ActorRole = actorRole,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            // The Before/After columns are jsonb, not plain text (see
            // AuditEventConfiguration), so a plain human readable line like
            // "Status: Active" has to be JSON-encoded first, otherwise Postgres
            // refuses it as invalid JSON. Callers just pass a plain string, this is
            // the one place that turns it into the JSON string literal Postgres wants.
            Before = before is null ? null : JsonSerializer.Serialize(before),
            After = after is null ? null : JsonSerializer.Serialize(after),
            Ip = ip,
            UserAgent = userAgent,
            CreatedAt = _clock.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
