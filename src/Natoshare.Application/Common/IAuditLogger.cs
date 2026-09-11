namespace Natoshare.Application.Common;

// Writes one row to the audit log. Call this any time something worth remembering
// happens, for example a login, a password change, or an admin action.
public interface IAuditLogger
{
    Task LogAsync(
        Guid? actorUserId,
        string actorRole,
        string action,
        string entityType,
        string entityId,
        string? ip,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
