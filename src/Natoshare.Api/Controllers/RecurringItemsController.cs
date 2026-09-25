using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Planning;

namespace Natoshare.Api.Controllers;

// Things that happen on a schedule, like rent or a subscription. Every action here
// needs an active Pro entitlement (trial or paid), see IEntitlementService.
[Route("api/v1/recurring")]
[Authorize(Policy = "RequireUser")]
public class RecurringItemsController : ApiControllerBase
{
    private readonly IRecurringItemService _recurringItemService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateRecurringItemRequest> _createValidator;
    private readonly IValidator<UpdateRecurringItemRequest> _updateValidator;

    public RecurringItemsController(
        IRecurringItemService recurringItemService,
        ICurrentUser currentUser,
        IValidator<CreateRecurringItemRequest> createValidator,
        IValidator<UpdateRecurringItemRequest> updateValidator)
    {
        _recurringItemService = recurringItemService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecurringItemDto>>> GetRecurringItems(CancellationToken cancellationToken)
    {
        var result = await _recurringItemService.ListAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<RecurringItemDto>> CreateRecurringItem(CreateRecurringItemRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _recurringItemService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{recurringItemId:guid}")]
    public async Task<ActionResult<RecurringItemDto>> UpdateRecurringItem(
        Guid recurringItemId, UpdateRecurringItemRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _recurringItemService.UpdateAsync(_currentUser.UserId, recurringItemId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{recurringItemId:guid}")]
    public async Task<IActionResult> DeleteRecurringItem(Guid recurringItemId, CancellationToken cancellationToken)
    {
        await _recurringItemService.DeleteAsync(_currentUser.UserId, recurringItemId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{recurringItemId:guid}/skip-next")]
    public async Task<ActionResult<RecurringItemDto>> SkipNext(Guid recurringItemId, CancellationToken cancellationToken)
    {
        var result = await _recurringItemService.SkipNextAsync(_currentUser.UserId, recurringItemId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("committed-total")]
    public async Task<ActionResult<CommittedTotalDto>> GetCommittedTotal(CancellationToken cancellationToken)
    {
        var result = await _recurringItemService.GetCommittedTotalAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
