using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;
using Natoshare.Application.Insights;

namespace Natoshare.Api.Controllers;

// The plain-English "how am I doing" view: a one-line summary and per-category
// pacing, built from the same numbers the balances screen uses.
[Route("api/v1/insights")]
[Authorize(Policy = "RequireUser")]
public class InsightsController : ApiControllerBase
{
    private readonly IInsightsService _insightsService;
    private readonly ICurrentUser _currentUser;

    public InsightsController(IInsightsService insightsService, ICurrentUser currentUser)
    {
        _insightsService = insightsService;
        _currentUser = currentUser;
    }

    [HttpGet("spending-summary")]
    public async Task<ActionResult<SpendingSummaryResult>> GetSpendingSummary(CancellationToken cancellationToken)
    {
        var result = await _insightsService.GetSpendingSummaryAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("pacing")]
    public async Task<ActionResult<IReadOnlyList<PacingInsightDto>>> GetPacing(CancellationToken cancellationToken)
    {
        var result = await _insightsService.GetPacingAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
