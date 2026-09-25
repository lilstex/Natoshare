using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Api.Controllers;

// A lightweight record of money actually invested somewhere real.
[Route("api/v1/investments")]
[Authorize(Policy = "RequireUser")]
public class InvestmentsController : ApiControllerBase
{
    private readonly IInvestmentLogService _investmentLogService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<CreateInvestmentLogRequest> _createValidator;

    public InvestmentsController(
        IInvestmentLogService investmentLogService, ICurrentUser currentUser, IValidator<CreateInvestmentLogRequest> createValidator)
    {
        _investmentLogService = investmentLogService;
        _currentUser = currentUser;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InvestmentLogDto>>> GetInvestments(
        DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await _investmentLogService.ListAsync(_currentUser.UserId, from, to, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<InvestmentLogDto>> CreateInvestment(CreateInvestmentLogRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _investmentLogService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<ActionResult<InvestmentSummaryDto>> GetSummary(int year, int month, CancellationToken cancellationToken)
    {
        var result = await _investmentLogService.GetSummaryAsync(_currentUser.UserId, year, month, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{investmentId:guid}")]
    public async Task<IActionResult> DeleteInvestment(Guid investmentId, CancellationToken cancellationToken)
    {
        await _investmentLogService.DeleteAsync(_currentUser.UserId, investmentId, cancellationToken);
        return NoContent();
    }
}
