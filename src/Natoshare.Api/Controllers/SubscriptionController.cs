using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Subscriptions;

namespace Natoshare.Api.Controllers;

// The stubbed subscription flow: asking to go Pro, seeing what happened to that
// request, and (for an admin) actually activating one, payments are not real yet
// (docs/00-plan.md section 5).
[Route("api/v1/subscription")]
[Authorize(Policy = "RequireUser")]
public class SubscriptionController : ApiControllerBase
{
    private readonly ISubscriptionService _subscriptionService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpgradeSubscriptionRequest> _upgradeValidator;

    public SubscriptionController(
        ISubscriptionService subscriptionService, ICurrentUser currentUser, IValidator<UpgradeSubscriptionRequest> upgradeValidator)
    {
        _subscriptionService = subscriptionService;
        _currentUser = currentUser;
        _upgradeValidator = upgradeValidator;
    }

    [HttpGet("status")]
    public async Task<ActionResult<SubscriptionStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.GetStatusAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("upgrade")]
    public async Task<ActionResult<UpgradeResultDto>> Upgrade(UpgradeSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await _upgradeValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _subscriptionService.UpgradeAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<SubscriptionRecordDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var result = await _subscriptionService.GetHistoryAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    // A different route prefix (/admin/subscriptions/... instead of
    // /subscription/...), kept on this same controller since it is still
    // subscription work, just gated to admins instead of the request's own owner.
    [HttpPost("/api/v1/admin/subscriptions/{reference}/activate")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Activate(string reference, ActivateSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await _subscriptionService.ActivateAsync(_currentUser.UserId, reference, request, cancellationToken);
        return NoContent();
    }
}
