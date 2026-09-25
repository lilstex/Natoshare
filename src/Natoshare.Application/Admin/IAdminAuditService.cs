namespace Natoshare.Application.Admin;

// Reads the audit log an admin already writes to just by using the other admin
// actions (and that the rest of the app writes to for logins, closes, and so on).
// This is read-only on purpose, an audit log you can edit is not an audit log
// (docs/04-admin-app.md section 2.4).
public interface IAdminAuditService
{
    Task<AdminAuditSearchResult> SearchAsync(
        Guid? actorUserId, string? entityType, string? action, DateTimeOffset? from, DateTimeOffset? to,
        int page, int pageSize, CancellationToken cancellationToken = default);
}
