using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers.Admin;

// The Users section of the admin app, see docs/04-admin-app.md section 2.2. Every
// route here is gated to the Admin role only, there is no owner-check fallback
// (docs/04-admin-app.md section 4), an admin can look at and act on any account.
[Route("api/v1/admin/users")]
[Authorize(Policy = "RequireAdmin")]
public class AdminUsersController : ApiControllerBase
{
    private readonly IAdminUserService _adminUserService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<SuspendUserRequest> _suspendValidator;
    private readonly IValidator<AdjustPlanRequest> _adjustPlanValidator;
    private readonly IValidator<HardDeleteUserRequest> _hardDeleteValidator;

    public AdminUsersController(
        IAdminUserService adminUserService,
        ICurrentUser currentUser,
        IValidator<SuspendUserRequest> suspendValidator,
        IValidator<AdjustPlanRequest> adjustPlanValidator,
        IValidator<HardDeleteUserRequest> hardDeleteValidator)
    {
        _adminUserService = adminUserService;
        _currentUser = currentUser;
        _suspendValidator = suspendValidator;
        _adjustPlanValidator = adjustPlanValidator;
        _hardDeleteValidator = hardDeleteValidator;
    }

    [HttpGet]
    public async Task<ActionResult<AdminUserListResult>> List(
        [FromQuery] string? q, [FromQuery] string? status, [FromQuery] string? plan,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var result = await _adminUserService.ListAsync(q, status, plan, page, pageSize, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<AdminUserDetailDto>> GetDetail(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.GetDetailAsync(userId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{userId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid userId, SuspendUserRequest request, CancellationToken cancellationToken)
    {
        await _suspendValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _adminUserService.SuspendAsync(_currentUser.UserId, userId, request, ClientIp, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid userId, CancellationToken cancellationToken)
    {
        await _adminUserService.ReactivateAsync(_currentUser.UserId, userId, ClientIp, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/reset-password")]
    public async Task<ActionResult<AdminResetPasswordResult>> ResetPassword(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.ResetPasswordAsync(_currentUser.UserId, userId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{userId:guid}/plan")]
    public async Task<IActionResult> AdjustPlan(Guid userId, AdjustPlanRequest request, CancellationToken cancellationToken)
    {
        await _adjustPlanValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _adminUserService.AdjustPlanAsync(_currentUser.UserId, userId, request, ClientIp, cancellationToken);
        return NoContent();
    }

    [HttpPost("{userId:guid}/export")]
    public async Task<ActionResult<AdminExportResult>> Export(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.ExportAsync(_currentUser.UserId, userId, ClientIp, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{userId:guid}/recompute-balances")]
    public async Task<ActionResult<RecomputeBalancesResult>> RecomputeBalances(Guid userId, CancellationToken cancellationToken)
    {
        var result = await _adminUserService.RecomputeBalancesAsync(userId, cancellationToken);
        return Ok(result);
    }

    // Hard delete. This is final, docs/04-admin-app.md section 4 requires the
    // frontend to make the admin type the account's email before calling this at
    // all, and HardDeleteUserRequestValidator plus AdminUserService both check the
    // same confirmText server side too, so a direct API call cannot skip it either.
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> HardDelete(Guid userId, HardDeleteUserRequest request, CancellationToken cancellationToken)
    {
        await _hardDeleteValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _adminUserService.HardDeleteAsync(_currentUser.UserId, userId, request, ClientIp, cancellationToken);
        return NoContent();
    }
}
