using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Api.Controllers;

// A calendar of upcoming dates the user should keep an eye on.
[Route("api/v1/obligations")]
[Authorize(Policy = "RequireUser")]
public class ObligationsController : ApiControllerBase
{
    private readonly IObligationsService _obligationsService;
    private readonly ICurrentUser _currentUser;

    public ObligationsController(IObligationsService obligationsService, ICurrentUser currentUser)
    {
        _obligationsService = obligationsService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ObligationItemDto>>> GetObligations(int days, CancellationToken cancellationToken)
    {
        var result = await _obligationsService.ListAsync(_currentUser.UserId, days <= 0 ? 30 : Math.Clamp(days, 1, 365), cancellationToken);
        return Ok(result);
    }
}
