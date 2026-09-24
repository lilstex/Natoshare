namespace Natoshare.Application.Admin;

// What the admin app's monitoring screen shows: is everything up, are jobs running,
// is the ledger still balanced, and is anything erroring right now. See
// docs/04-admin-app.md section 2.5.
public interface IAdminMonitoringService
{
    Task<AdminMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default);

    Task<AdminHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);

    AdminJobsDto GetJobs();

    Task<IntegrityCheckStatusDto> GetIntegrityStatusAsync(CancellationToken cancellationToken = default);

    IReadOnlyList<AdminErrorLogEntryDto> GetRecentErrors();
}
