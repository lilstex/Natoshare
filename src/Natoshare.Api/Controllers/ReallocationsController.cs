using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;

namespace Natoshare.Api.Controllers;

// A deliberate move of money from one account to another. Unlike an expense, this
// can never leave the source account short.
[Route("api/v1/reallocations")]
[Authorize(Policy = "RequireUser")]
public class ReallocationsController : ApiControllerBase
{
    private readonly IReallocationService _reallocationService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateReallocationRequest> _createValidator;

    public ReallocationsController(
        IReallocationService reallocationService, ICurrentUser currentUser, IValidator<CreateReallocationRequest> createValidator)
    {
        _reallocationService = reallocationService;
        _currentUser = currentUser;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReallocationDto>>> GetReallocations(
        DateOnly? from, DateOnly? to, string? reason, CancellationToken cancellationToken)
    {
        var result = await _reallocationService.ListAsync(_currentUser.UserId, from, to, reason, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<ReallocationDto>> CreateReallocation(CreateReallocationRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _reallocationService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }
}
