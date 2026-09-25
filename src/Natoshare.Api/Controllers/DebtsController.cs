using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Api.Controllers;

// Money the user borrowed from someone else.
[Route("api/v1/debts")]
[Authorize(Policy = "RequireUser")]
public class DebtsController : ApiControllerBase
{
    private readonly IDebtInService _debtInService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateDebtInRequest> _createValidator;
    private readonly IValidator<CreateDebtRepaymentRequest> _repaymentValidator;
    private readonly IValidator<UpdateDebtInRequest> _updateValidator;

    public DebtsController(
        IDebtInService debtInService,
        ICurrentUser currentUser,
        IValidator<CreateDebtInRequest> createValidator,
        IValidator<CreateDebtRepaymentRequest> repaymentValidator,
        IValidator<UpdateDebtInRequest> updateValidator)
    {
        _debtInService = debtInService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _repaymentValidator = repaymentValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DebtInDto>>> GetDebts(string? status, CancellationToken cancellationToken)
    {
        var result = await _debtInService.ListAsync(_currentUser.UserId, status, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DebtInDto>> CreateDebt(CreateDebtInRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _debtInService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{debtId:guid}/repayments")]
    public async Task<ActionResult<DebtInDto>> AddRepayment(Guid debtId, CreateDebtRepaymentRequest request, CancellationToken cancellationToken)
    {
        await _repaymentValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _debtInService.AddRepaymentAsync(_currentUser.UserId, debtId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{debtId:guid}")]
    public async Task<ActionResult<DebtInDto>> UpdateDebt(Guid debtId, UpdateDebtInRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _debtInService.UpdateAsync(_currentUser.UserId, debtId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{debtId:guid}")]
    public async Task<IActionResult> DeleteDebt(Guid debtId, CancellationToken cancellationToken)
    {
        await _debtInService.DeleteAsync(_currentUser.UserId, debtId, cancellationToken);
        return NoContent();
    }
}
