using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Admin;

namespace Natoshare.Api.Controllers.Admin;

// Read-only by design, docs/04-admin-app.md section 2.4: an audit log you can edit
// is not an audit log.
[Route("api/v1/admin/audit")]
[Authorize(Policy = "RequireAdmin")]
public class AdminAuditController : ApiControllerBase
{
    private readonly IAdminAuditService _adminAuditService;

    public AdminAuditController(IAdminAuditService adminAuditService)
    {
        _adminAuditService = adminAuditService;
    }

    [HttpGet]
    public async Task<ActionResult<AdminAuditSearchResult>> Search(
        [FromQuery] Guid? actorUserId, [FromQuery] string? entityType, [FromQuery] string? action,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var result = await _adminAuditService.SearchAsync(actorUserId, entityType, action, from, to, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
