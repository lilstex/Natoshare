using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Filters;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;

namespace Natoshare.Api.Controllers;

// Money going out. Never blocked, even when it pushes a category into deficit, the
// response just tells the caller so honestly.
[Route("api/v1/expenses")]
[Authorize(Policy = "RequireUser")]
public class ExpensesController : ApiControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<LogExpenseRequest> _logValidator;
    private readonly IValidator<UpdateExpenseRequest> _updateValidator;

    public ExpensesController(
        IExpenseService expenseService,
        ICurrentUser currentUser,
        IValidator<LogExpenseRequest> logValidator,
        IValidator<UpdateExpenseRequest> updateValidator)
    {
        _expenseService = expenseService;
        _currentUser = currentUser;
        _logValidator = logValidator;
        _updateValidator = updateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExpenseDto>>> GetExpenses(
        string? source, Guid? categoryId, string? tag, DateOnly? from, DateOnly? to,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _expenseService.ListAsync(
            _currentUser.UserId, source, categoryId, tag, from, to, page, Math.Clamp(pageSize, 1, 100), cancellationToken);
        return Ok(result);
    }

    // This has to come before {expenseId:guid} in route matching, ASP.NET Core
    // handles that fine on its own because "monthly-total" cannot match a guid.
    [HttpGet("monthly-total")]
    public async Task<ActionResult<decimal>> GetMonthlyTotal(Guid? categoryId, int year, int month, CancellationToken cancellationToken)
    {
        var result = await _expenseService.GetMonthlyTotalAsync(_currentUser.UserId, categoryId, year, month, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{expenseId:guid}")]
    public async Task<ActionResult<ExpenseDto>> GetOne(Guid expenseId, CancellationToken cancellationToken)
    {
        var result = await _expenseService.GetAsync(_currentUser.UserId, expenseId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [ServiceFilter(typeof(IdempotencyActionFilter))]
    public async Task<ActionResult<LogExpenseResult>> LogExpense(LogExpenseRequest request, CancellationToken cancellationToken)
    {
        await _logValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _expenseService.CreateAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{expenseId:guid}")]
    public async Task<ActionResult<LogExpenseResult>> UpdateExpense(Guid expenseId, UpdateExpenseRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _expenseService.UpdateAsync(_currentUser.UserId, expenseId, request, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{expenseId:guid}")]
    public async Task<IActionResult> DeleteExpense(Guid expenseId, CancellationToken cancellationToken)
    {
        await _expenseService.DeleteAsync(_currentUser.UserId, expenseId, cancellationToken);
        return NoContent();
    }
}
