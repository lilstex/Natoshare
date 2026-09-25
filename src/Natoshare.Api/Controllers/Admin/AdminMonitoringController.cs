using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Admin;
using Natoshare.Application.Auth;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers.Admin;

// The Monitoring section of the admin app, see docs/04-admin-app.md section 2.5.
[Route("api/v1/admin")]
[Authorize(Policy = "RequireAdmin")]
public class AdminMonitoringController : ApiControllerBase
{
    private readonly IAdminMonitoringService _adminMonitoringService;
    private readonly ITokenService _tokenService;
    private readonly ICurrentUser _currentUser;

    public AdminMonitoringController(IAdminMonitoringService adminMonitoringService, ITokenService tokenService, ICurrentUser currentUser)
    {
        _adminMonitoringService = adminMonitoringService;
        _tokenService = tokenService;
        _currentUser = currentUser;
    }

    // Mints the narrow, short-lived token the admin app's "Open Hangfire dashboard"
    // link actually uses (see AdminOnlyDashboardAuthFilter), instead of that link
    // carrying the caller's real, full-privilege session token in a URL. Fixed
    // during Phase 11's security review, see 05-implementation-phases.md.
    [HttpGet("hangfire-token")]
    public ActionResult<object> GetHangfireDashboardToken()
    {
        var token = _tokenService.CreateHangfireDashboardToken(_currentUser.UserId);
        return Ok(new { token });
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<AdminMetricsDto>> GetMetrics(CancellationToken cancellationToken)
    {
        var result = await _adminMonitoringService.GetMetricsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("health")]
    public async Task<ActionResult<AdminHealthDto>> GetHealth(CancellationToken cancellationToken)
    {
        var result = await _adminMonitoringService.GetHealthAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("jobs")]
    public ActionResult<AdminJobsDto> GetJobs()
    {
        return Ok(_adminMonitoringService.GetJobs());
    }

    [HttpGet("integrity-check")]
    public async Task<ActionResult<IntegrityCheckStatusDto>> GetIntegrityStatus(CancellationToken cancellationToken)
    {
        var result = await _adminMonitoringService.GetIntegrityStatusAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("errors")]
    public ActionResult<IReadOnlyList<AdminErrorLogEntryDto>> GetRecentErrors()
    {
        return Ok(_adminMonitoringService.GetRecentErrors());
    }
}
