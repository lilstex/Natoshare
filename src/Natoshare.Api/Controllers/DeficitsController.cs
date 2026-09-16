using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;

namespace Natoshare.Api.Controllers;

// Covering a category's shortfall right now, from somewhere that actually has the
// money.
[Route("api/v1/deficits")]
[Authorize(Policy = "RequireUser")]
public class DeficitsController : ApiControllerBase
{
    private readonly IDeficitService _deficitService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<ResolveDeficitRequest> _resolveValidator;

    public DeficitsController(IDeficitService deficitService, ICurrentUser currentUser, IValidator<ResolveDeficitRequest> resolveValidator)
    {
        _deficitService = deficitService;
        _currentUser = currentUser;
        _resolveValidator = resolveValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DeficitListItemDto>>> GetDeficits(
        int? year, int? month, string? status, CancellationToken cancellationToken)
    {
        var result = await _deficitService.ListAsync(_currentUser.UserId, year, month, status, cancellationToken);
        return Ok(result);
    }

    [HttpPost("resolve")]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<DeficitResolutionDto>> Resolve(ResolveDeficitRequest request, CancellationToken cancellationToken)
    {
        await _resolveValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _deficitService.ResolveAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<DeficitHistoryItemDto>>> GetHistory(
        Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await _deficitService.GetHistoryAsync(_currentUser.UserId, categoryId, from, to, cancellationToken);
        return Ok(result);
    }
}
