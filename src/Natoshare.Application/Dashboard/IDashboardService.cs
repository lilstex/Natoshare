namespace Natoshare.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}
