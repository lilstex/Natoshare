using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Api.Controllers;

// The one-number answer to "where do I really stand right now".
[Route("api/v1/net-position")]
[Authorize(Policy = "RequireUser")]
public class NetPositionController : ApiControllerBase
{
    private readonly INetPositionService _netPositionService;
    private readonly ICurrentUser _currentUser;

    public NetPositionController(INetPositionService netPositionService, ICurrentUser currentUser)
    {
        _netPositionService = netPositionService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<NetPositionDto>> GetNetPosition(CancellationToken cancellationToken)
    {
        var result = await _netPositionService.GetAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
