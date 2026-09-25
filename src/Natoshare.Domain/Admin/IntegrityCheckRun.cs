namespace Natoshare.Domain.Admin;

// One row for every time the nightly ledger integrity check ran, so the admin app's
// monitoring screen can show "when did this last run, and did it find anything"
// without having to dig through log files.
public class IntegrityCheckRun
{
    public Guid Id { get; init; }

    public DateTimeOffset RanAt { get; set; }

    public int CheckedCount { get; set; }

    public int DriftCount { get; set; }
}
