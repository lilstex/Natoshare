using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;

namespace Natoshare.Api.Controllers;

// A user's own labels for grouping expenses, like "work" or "birthday".
[Route("api/v1/tags")]
[Authorize(Policy = "RequireUser")]
public class TagsController : ApiControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ICurrentUser _currentUser;

    public TagsController(IExpenseService expenseService, ICurrentUser currentUser)
    {
        _expenseService = expenseService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetTags(CancellationToken cancellationToken)
    {
        var result = await _expenseService.GetTagsAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
