using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers.Admin;

[Route("api/v1/admin/feature-flags")]
[Authorize(Policy = "RequireAdmin")]
public class AdminFeatureFlagsController : ApiControllerBase
{
    private readonly IAdminFeatureFlagService _adminFeatureFlagService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<PatchFeatureFlagRequest> _validator;

    public AdminFeatureFlagsController(
        IAdminFeatureFlagService adminFeatureFlagService, ICurrentUser currentUser, IValidator<PatchFeatureFlagRequest> validator)
    {
        _adminFeatureFlagService = adminFeatureFlagService;
        _currentUser = currentUser;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminFeatureFlagDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _adminFeatureFlagService.ListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{key}")]
    public async Task<ActionResult<AdminFeatureFlagDto>> Update(string key, PatchFeatureFlagRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _adminFeatureFlagService.UpdateAsync(key, request, _currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
