using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;

namespace Natoshare.Api.Controllers;

// Money coming in. Allocatable income splits across categories the moment it is
// logged, Flexible income tops up the shared Flexible Pool.
[Route("api/v1/income")]
[Authorize(Policy = "RequireUser")]
public class IncomeController : ApiControllerBase
{
    private readonly IIncomeService _incomeService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<LogIncomeRequest> _logValidator;
    private readonly IValidator<UpdateIncomeRequest> _updateValidator;

    public IncomeController(
        IIncomeService incomeService,
        ICurrentUser currentUser,
        IValidator<LogIncomeRequest> logValidator,
        IValidator<UpdateIncomeRequest> updateValidator)
    {
        _incomeService = incomeService;
        _currentUser = currentUser;
        _logValidator = logValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncomeDto>>> GetIncome(
        string? type, DateOnly? from, DateOnly? to, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _incomeService.ListAsync(_currentUser.UserId, type, from, to, page, Math.Clamp(pageSize, 1, 100), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{incomeId:guid}")]
    public async Task<ActionResult<IncomeDto>> GetOne(Guid incomeId, CancellationToken cancellationToken)
    {
        var result = await _incomeService.GetAsync(_currentUser.UserId, incomeId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<IncomeDto>> LogIncome(LogIncomeRequest request, CancellationToken cancellationToken)
    {
        await _logValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _incomeService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{incomeId:guid}")]
    public async Task<ActionResult<IncomeDto>> UpdateIncome(Guid incomeId, UpdateIncomeRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _incomeService.UpdateAsync(_currentUser.UserId, incomeId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{incomeId:guid}")]
    public async Task<IActionResult> DeleteIncome(Guid incomeId, CancellationToken cancellationToken)
    {
        await _incomeService.DeleteAsync(_currentUser.UserId, incomeId, cancellationToken);
        return NoContent();
    }
}
