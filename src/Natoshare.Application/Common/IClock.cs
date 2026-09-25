namespace Natoshare.Application.Common;

// Lets us ask "what time is it right now" through an interface instead of calling
// DateTimeOffset.UtcNow directly everywhere. This way, a test can control time instead
// of depending on whatever the real clock says when the test happens to run.
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
