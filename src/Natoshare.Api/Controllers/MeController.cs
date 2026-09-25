using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Me;

namespace Natoshare.Api.Controllers;

// Everything a logged in user can do to their own profile and account.
[Route("api/v1/me")]
[Authorize(Policy = "RequireUser")]
public class MeController : ApiControllerBase
{
    private readonly IMeService _meService;
    private readonly ICurrentUser _currentUser;
    private readonly IEntitlementService _entitlementService;
    private readonly IValidator<UpdateMeRequest> _updateMeValidator;
    private readonly IValidator<UpdateCurrencyRequest> _updateCurrencyValidator;
    private readonly IValidator<DeleteAccountRequest> _deleteAccountValidator;

    public MeController(
        IMeService meService,
        ICurrentUser currentUser,
        IEntitlementService entitlementService,
        IValidator<UpdateMeRequest> updateMeValidator,
        IValidator<UpdateCurrencyRequest> updateCurrencyValidator,
        IValidator<DeleteAccountRequest> deleteAccountValidator)
    {
        _meService = meService;
        _currentUser = currentUser;
        _entitlementService = entitlementService;
        _updateMeValidator = updateMeValidator;
        _updateCurrencyValidator = updateCurrencyValidator;
        _deleteAccountValidator = deleteAccountValidator;
    }

    [HttpGet("entitlements")]
    public async Task<ActionResult<PlanEntitlements>> GetEntitlements(CancellationToken cancellationToken)
    {
        var result = await _entitlementService.ResolveAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<GetMeResult>> GetMe(CancellationToken cancellationToken)
    {
        var result = await _meService.GetMeAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpPatch]
    public async Task<ActionResult<GetMeResult>> UpdateMe(UpdateMeRequest request, CancellationToken cancellationToken)
    {
        await _updateMeValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _meService.UpdateMeAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("currency")]
    public async Task<ActionResult<GetMeResult>> UpdateCurrency(UpdateCurrencyRequest request, CancellationToken cancellationToken)
    {
        await _updateCurrencyValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _meService.UpdateCurrencyAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("export")]
    public async Task<IActionResult> RequestExport(CancellationToken cancellationToken)
    {
        var exportId = await _meService.RequestExportAsync(_currentUser.UserId, cancellationToken);
        return Accepted(new { exportId });
    }

    [HttpGet("export/{exportId:guid}")]
    public async Task<ActionResult<ExportStatusResult>> GetExportStatus(Guid exportId, CancellationToken cancellationToken)
    {
        var result = await _meService.GetExportStatusAsync(_currentUser.UserId, exportId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete]
    public async Task<IActionResult> DeleteAccount(DeleteAccountRequest request, CancellationToken cancellationToken)
    {
        await _deleteAccountValidator.ValidateOrThrowAsync(request, cancellationToken);
        await _meService.DeleteAccountAsync(_currentUser.UserId, request, ClientIp, cancellationToken);
        return Accepted();
    }
}
