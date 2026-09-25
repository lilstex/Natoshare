namespace Natoshare.Application.Common;

// Writes one row to the audit log. Call this any time something worth remembering
// happens, for example a login, a password change, or an admin action.
public interface IAuditLogger
{
    // "before" and "after" are a short, human readable line each, for example
    // "Status: Active" and "Status: Suspended". They stay null for events where
    // there is no meaningful before/after state (like a login).
    Task LogAsync(
        Guid? actorUserId,
        string actorRole,
        string action,
        string entityType,
        string entityId,
        string? ip,
        string? userAgent,
        string? before = null,
        string? after = null,
        CancellationToken cancellationToken = default);
}
