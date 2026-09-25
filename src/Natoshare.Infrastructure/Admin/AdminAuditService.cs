using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Admin;
using Natoshare.Domain.Audit;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Admin;

public class AdminAuditService : IAdminAuditService
{
    private readonly NatoshareDbContext _dbContext;

    public AdminAuditService(NatoshareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AdminAuditSearchResult> SearchAsync(
        Guid? actorUserId, string? entityType, string? action, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // Clamped server side, not just trusted from the query string, so a
        // careless or malicious ?pageSize=999999 cannot force a full table scan.
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _dbContext.AuditEvents.AsQueryable();

        if (actorUserId is not null)
        {
            query = query.Where(a => a.ActorUserId == actorUserId);
        }

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            query = query.Where(a => a.EntityType == entityType);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (from is not null)
        {
            query = query.Where(a => a.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(a => a.CreatedAt <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AdminAuditSearchResult(rows.Select(ToDto).ToList(), totalCount, page, pageSize);
    }

    private static AdminAuditEventDto ToDto(AuditEvent e) => new(
        e.Id, e.ActorUserId, e.ActorRole, e.Action, e.EntityType, e.EntityId, AuditJson.Decode(e.Before), AuditJson.Decode(e.After), e.Ip, e.CreatedAt);
}
