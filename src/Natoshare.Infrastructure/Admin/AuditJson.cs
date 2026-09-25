using System.Text.Json;

namespace Natoshare.Infrastructure.Admin;

// AuditEvent.Before/After are stored as jsonb (a JSON string literal holding a plain
// human readable line, see AuditLogger). This turns that back into a normal string
// for anything that reads the audit log back out, like the admin app's screens.
public static class AuditJson
{
    public static string? Decode(string? raw) => raw is null ? null : JsonSerializer.Deserialize<string>(raw);
}
