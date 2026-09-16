using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Months;

namespace Natoshare.Api.Controllers;

// The close-month ritual: seeing where a month stands, confirming a fixed account
// transfer, and closing the month for good once every deficit is resolved.
[Route("api/v1/months")]
[Authorize(Policy = "RequireUser")]
public class MonthsController : ApiControllerBase
{
    private readonly IMonthLifecycleService _monthLifecycleService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<ConfirmFixedAccountRequest> _confirmFixedAccountValidator;
    private readonly IValidator<CloseMonthRequest> _closeValidator;
    private readonly IValidator<ReopenMonthRequest> _reopenValidator;

    public MonthsController(
        IMonthLifecycleService monthLifecycleService,
        ICurrentUser currentUser,
        IValidator<ConfirmFixedAccountRequest> confirmFixedAccountValidator,
        IValidator<CloseMonthRequest> closeValidator,
        IValidator<ReopenMonthRequest> reopenValidator)
    {
        _monthLifecycleService = monthLifecycleService;
        _currentUser = currentUser;
        _confirmFixedAccountValidator = confirmFixedAccountValidator;
        _closeValidator = closeValidator;
        _reopenValidator = reopenValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MonthSummaryListItemDto>>> GetMonths(CancellationToken cancellationToken)
    {
        var result = await _monthLifecycleService.ListAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{year:int}/{month:int}")]
    public async Task<ActionResult<MonthDetailDto>> GetMonth(int year, int month, CancellationToken cancellationToken)
    {
        var result = await _monthLifecycleService.GetDetailAsync(_currentUser.UserId, year, month, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{year:int}/{month:int}/open")]
    public async Task<ActionResult<MonthDetailDto>> OpenEarly(int year, int month, CancellationToken cancellationToken)
    {
        var result = await _monthLifecycleService.OpenEarlyAsync(_currentUser.UserId, year, month, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{year:int}/{month:int}/close-preview")]
    public async Task<ActionResult<ClosePreviewResult>> GetClosePreview(int year, int month, CancellationToken cancellationToken)
    {
        var result = await _monthLifecycleService.GetClosePreviewAsync(_currentUser.UserId, year, month, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{year:int}/{month:int}/confirm-fixed-account")]
    public async Task<ActionResult<CategoryMonthDetailDto>> ConfirmFixedAccount(
        int year, int month, ConfirmFixedAccountRequest request, CancellationToken cancellationToken)
    {
        await _confirmFixedAccountValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _monthLifecycleService.ConfirmFixedAccountAsync(_currentUser.UserId, year, month, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{year:int}/{month:int}/close")]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<CloseMonthResult>> Close(int year, int month, CloseMonthRequest request, CancellationToken cancellationToken)
    {
        await _closeValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _monthLifecycleService.CloseAsync(_currentUser.UserId, year, month, request, cancellationToken);
        return Ok(result);
    }

    // A different route prefix entirely (/admin/months/... instead of /months/...),
    // kept on this same controller since it is still month-lifecycle work, just
    // gated to admins instead of the month's own owner.
    [HttpPost("/api/v1/admin/months/{userId:guid}/{year:int}/{month:int}/reopen")]
    [Authorize(Policy = "RequireAdmin")]
    public async Task<IActionResult> Reopen(Guid userId, int year, int month, ReopenMonthRequest request, CancellationToken cancellationToken)
    {
        await _reopenValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _monthLifecycleService.ReopenAsync(_currentUser.UserId, userId, year, month, request.Reason, cancellationToken);
        return NoContent();
    }
}
