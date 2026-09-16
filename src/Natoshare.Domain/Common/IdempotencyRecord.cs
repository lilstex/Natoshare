namespace Natoshare.Domain.Common;

// Remembers the result of a write the client marked with an Idempotency-Key header, so
// if the same request comes in twice (a slow network making someone tap "save" again,
// a retried request), the second one just gets back the first result instead of
// logging the same income or expense a second time.
public class IdempotencyRecord
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string Key { get; init; } = "";

    public int ResponseStatusCode { get; init; }

    public string ResponseBody { get; init; } = "";

    public DateTimeOffset CreatedAt { get; init; }
}
