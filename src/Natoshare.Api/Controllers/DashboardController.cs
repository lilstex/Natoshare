using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;
using Natoshare.Application.Dashboard;

namespace Natoshare.Api.Controllers;

// The one-call version of the dashboard's hero screen.
[Route("api/v1/dashboard")]
[Authorize(Policy = "RequireUser")]
public class DashboardController : ApiControllerBase
{
    private readonly IDashboardService _dashboardService;
    private readonly ICurrentUser _currentUser;

    public DashboardController(IDashboardService dashboardService, ICurrentUser currentUser)
    {
        _dashboardService = dashboardService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> GetDashboard(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
