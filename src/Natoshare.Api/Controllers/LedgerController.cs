using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;

namespace Natoshare.Api.Controllers;

// The raw, append-only feed every balance in Natoshare is built from. Read-only,
// mostly useful for support or someone who wants to see exactly what happened.
[Route("api/v1/ledger")]
[Authorize(Policy = "RequireUser")]
public class LedgerController : ApiControllerBase
{
    private readonly ILedgerReadService _ledgerReadService;
    private readonly ICurrentUser _currentUser;

    public LedgerController(ILedgerReadService ledgerReadService, ICurrentUser currentUser)
    {
        _ledgerReadService = ledgerReadService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LedgerEntryDto>>> GetLedger(
        string? account, string? entryType, DateOnly? from, DateOnly? to,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var result = await _ledgerReadService.ListAsync(
            _currentUser.UserId, account, entryType, from, to, page, Math.Clamp(pageSize, 1, 100), cancellationToken);
        return Ok(result);
    }
}
