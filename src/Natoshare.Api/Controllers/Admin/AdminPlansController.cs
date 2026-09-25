using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers.Admin;

[Route("api/v1/admin/plans")]
[Authorize(Policy = "RequireAdmin")]
public class AdminPlansController : ApiControllerBase
{
    private readonly IAdminPlanService _adminPlanService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<PatchPlanConfigRequest> _validator;

    public AdminPlansController(IAdminPlanService adminPlanService, ICurrentUser currentUser, IValidator<PatchPlanConfigRequest> validator)
    {
        _adminPlanService = adminPlanService;
        _currentUser = currentUser;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminPlanConfigDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _adminPlanService.ListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{plan}")]
    public async Task<ActionResult<AdminPlanConfigDto>> Update(string plan, PatchPlanConfigRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _adminPlanService.UpdateAsync(plan, request, _currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
