using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Api.Controllers;

// Money the user lent to someone else.
[Route("api/v1/loans-out")]
[Authorize(Policy = "RequireUser")]
public class LoansOutController : ApiControllerBase
{
    private readonly ILoanOutService _loanOutService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateLoanOutRequest> _createValidator;
    private readonly IValidator<CreateLoanRepaymentRequest> _repaymentValidator;
    private readonly IValidator<UpdateLoanOutRequest> _updateValidator;

    public LoansOutController(
        ILoanOutService loanOutService,
        ICurrentUser currentUser,
        IValidator<CreateLoanOutRequest> createValidator,
        IValidator<CreateLoanRepaymentRequest> repaymentValidator,
        IValidator<UpdateLoanOutRequest> updateValidator)
    {
        _loanOutService = loanOutService;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _repaymentValidator = repaymentValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LoanOutDto>>> GetLoans(string? status, CancellationToken cancellationToken)
    {
        var result = await _loanOutService.ListAsync(_currentUser.UserId, status, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<LoanOutDto>> CreateLoan(CreateLoanOutRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _loanOutService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{loanId:guid}/repayments")]
    public async Task<ActionResult<LoanOutDto>> AddRepayment(Guid loanId, CreateLoanRepaymentRequest request, CancellationToken cancellationToken)
    {
        await _repaymentValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _loanOutService.AddRepaymentAsync(_currentUser.UserId, loanId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{loanId:guid}")]
    public async Task<ActionResult<LoanOutDto>> UpdateLoan(Guid loanId, UpdateLoanOutRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _loanOutService.UpdateAsync(_currentUser.UserId, loanId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{loanId:guid}")]
    public async Task<IActionResult> DeleteLoan(Guid loanId, CancellationToken cancellationToken)
    {
        await _loanOutService.DeleteAsync(_currentUser.UserId, loanId, cancellationToken);
        return NoContent();
    }
}
