using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Api.Controllers;

// Money the user has told someone they will give them, but has not handed over yet.
[Route("api/v1/promises")]
[Authorize(Policy = "RequireUser")]
public class PromisesController : ApiControllerBase
{
    private readonly IPromiseService _promiseService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreatePromiseRequest> _createValidator;
    private readonly IValidator<CreatePromiseRedemptionRequest> _redemptionValidator;
    private readonly IValidator<UpdatePromiseRequest> _updateValidator;

    public PromisesController(
        IPromiseService promiseService,
        ICurrentUser currentUser,
        IValidator<CreatePromiseRequest> createValidator,
        IValidator<CreatePromiseRedemptionRequest> redemptionValidator,
        IValidator<UpdatePromiseRequest> updateValidator)
    {
        _promiseService = promiseService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _redemptionValidator = redemptionValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PromiseDto>>> GetPromises(string? status, CancellationToken cancellationToken)
    {
        var result = await _promiseService.ListAsync(_currentUser.UserId, status, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PromiseDto>> CreatePromise(CreatePromiseRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _promiseService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{promiseId:guid}/redemptions")]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<PromiseDto>> AddRedemption(
        Guid promiseId, CreatePromiseRedemptionRequest request, CancellationToken cancellationToken)
    {
        await _redemptionValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _promiseService.AddRedemptionAsync(_currentUser.UserId, promiseId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{promiseId:guid}")]
    public async Task<ActionResult<PromiseDto>> UpdatePromise(Guid promiseId, UpdatePromiseRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _promiseService.UpdateAsync(_currentUser.UserId, promiseId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{promiseId:guid}")]
    public async Task<IActionResult> DeletePromise(Guid promiseId, CancellationToken cancellationToken)
    {
        await _promiseService.DeleteAsync(_currentUser.UserId, promiseId, cancellationToken);
        return NoContent();
    }
}
