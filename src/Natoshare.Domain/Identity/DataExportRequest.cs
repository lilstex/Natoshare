namespace Natoshare.Domain.Identity;

public enum DataExportStatus
{
    Pending,
    Ready,
    Failed
}

// A request to download a copy of everything Natoshare has about one user.
// Right now nothing builds the actual file yet, that only makes sense once there is
// real data (income, expenses, and so on) to export. This just keeps an honest record
// of the request so the API has something real to say when asked about it.
public class DataExportRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DataExportStatus Status { get; set; } = DataExportStatus.Pending;
    public string? DownloadUrl { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
