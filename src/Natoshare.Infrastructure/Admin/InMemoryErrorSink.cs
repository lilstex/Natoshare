using System.Collections.Concurrent;
using Natoshare.Application.Admin;
using Serilog.Core;
using Serilog.Events;

namespace Natoshare.Infrastructure.Admin;

// Keeps the last 100 Error/Fatal log lines in memory so the admin app's monitoring
// screen has something real to show for "what has broken recently", without needing
// a full log aggregator like Seq wired up (that is real infrastructure work, out of
// scope for v1, see docs/04-admin-app.md section 2.5's "Errors" bullet).
public class InMemoryErrorSink : ILogEventSink
{
    private const int Capacity = 100;
    private static readonly ConcurrentQueue<AdminErrorLogEntryDto> Entries = new();

    public void Emit(LogEvent logEvent)
    {
        if (logEvent.Level is not (LogEventLevel.Error or LogEventLevel.Fatal))
        {
            return;
        }

        Entries.Enqueue(new AdminErrorLogEntryDto(
            logEvent.Timestamp, logEvent.Level.ToString(), logEvent.RenderMessage(), logEvent.Exception?.ToString()));

        while (Entries.Count > Capacity && Entries.TryDequeue(out _))
        {
        }
    }

    public static IReadOnlyList<AdminErrorLogEntryDto> GetRecent() => Entries.Reverse().ToList();
}
