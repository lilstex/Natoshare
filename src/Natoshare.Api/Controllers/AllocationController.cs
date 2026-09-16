using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers;

// How much a user earns and how it is split across categories, month by month.
[Route("api/v1/allocation")]
[Authorize(Policy = "RequireUser")]
public class AllocationController : ApiControllerBase
{
    private readonly IAllocationService _allocationService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateAllocationVersionRequest> _createVersionValidator;
    private readonly IValidator<AllocationPreviewRequest> _previewValidator;

    public AllocationController(
        IAllocationService allocationService,
        ICurrentUser currentUser,
        IValidator<CreateAllocationVersionRequest> createVersionValidator,
        IValidator<AllocationPreviewRequest> previewValidator)
    {
        _allocationService = allocationService;
        _currentUser = currentUser;
        _createVersionValidator = createVersionValidator;
        _previewValidator = previewValidator;
    }

    // The split that applies right now, in the user's own current month.
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await _allocationService.GetCurrentAsync(_currentUser.UserId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    // Every split the user has ever set up, newest first, so they can see their
    // history of changes.
    [HttpGet("versions")]
    public async Task<ActionResult<IReadOnlyList<AllocationVersionResult>>> GetVersions(CancellationToken cancellationToken)
    {
        var result = await _allocationService.GetVersionsAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("versions/{versionId:guid}")]
    public async Task<ActionResult<AllocationVersionResult>> GetVersion(Guid versionId, CancellationToken cancellationToken)
    {
        var result = await _allocationService.GetVersionAsync(_currentUser.UserId, versionId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("versions")]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<AllocationVersionResult>> CreateVersion(CreateAllocationVersionRequest request, CancellationToken cancellationToken)
    {
        await _createVersionValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _allocationService.CreateVersionAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    // Lets the frontend show naira/dollar amounts per category before the user
    // actually saves anything. This is a query, not a write, so a plain POST body is
    // used instead of the api-surface doc's original query string design, an array of
    // objects does not fit cleanly in a query string.
    [HttpPost("preview")]
    public async Task<ActionResult<AllocationPreviewResult>> Preview(AllocationPreviewRequest request, CancellationToken cancellationToken)
    {
        await _previewValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = _allocationService.Preview(request);
        return Ok(result);
    }
}
