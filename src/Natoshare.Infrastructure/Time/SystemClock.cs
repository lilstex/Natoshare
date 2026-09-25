using Natoshare.Application.Common;

namespace Natoshare.Infrastructure.Time;

// The real clock, it just reads the computer's actual time. Tests use a fake one
// instead so they can control what "now" means.
public class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
