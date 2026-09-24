using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Admin;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers.Admin;

[Route("api/v1/admin/settings")]
[Authorize(Policy = "RequireAdmin")]
public class AdminSettingsController : ApiControllerBase
{
    private readonly ISystemSettingsService _systemSettingsService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<PatchSettingRequest> _validator;

    public AdminSettingsController(ISystemSettingsService systemSettingsService, ICurrentUser currentUser, IValidator<PatchSettingRequest> validator)
    {
        _systemSettingsService = systemSettingsService;
        _currentUser = currentUser;
        _validator = validator;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> List(CancellationToken cancellationToken)
    {
        var result = await _systemSettingsService.ListAsync(cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{key}")]
    public async Task<ActionResult<SystemSettingDto>> Update(string key, PatchSettingRequest request, CancellationToken cancellationToken)
    {
        await _validator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _systemSettingsService.SetAsync(key, request, _currentUser.UserId, cancellationToken);
        return Ok(result);
    }
}
