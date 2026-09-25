using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;

namespace Natoshare.Api.Controllers;

// What every category, the Flexible Pool, and the whole account actually have right
// now. This is the screen a user looks at the most, so everything here is worked out
// fresh from the ledger and the current month, never stored on its own.
[Route("api/v1/balances")]
[Authorize(Policy = "RequireUser")]
public class BalancesController : ApiControllerBase
{
    private readonly IBalanceService _balanceService;
    private readonly ICurrentUser _currentUser;

    public BalancesController(IBalanceService balanceService, ICurrentUser currentUser)
    {
        _balanceService = balanceService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<BalancesResult>> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await _balanceService.GetCurrentAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<BalanceHistoryItemDto>>> GetHistory(
        Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var result = await _balanceService.GetHistoryAsync(_currentUser.UserId, categoryId, from, to, cancellationToken);
        return Ok(result);
    }
}
