using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Budgeting;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers;

// Everything the onboarding wizard needs: currency options, budget templates, how far
// the user has gotten, and the final "finish onboarding" submit.
[Route("api/v1/onboarding")]
[Authorize(Policy = "RequireUser")]
public class OnboardingController : ApiControllerBase
{
    private readonly ICurrencyCatalog _currencyCatalog;
    private readonly IOnboardingService _onboardingService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<OnboardingCompleteRequest> _completeValidator;

    public OnboardingController(
        ICurrencyCatalog currencyCatalog,
        IOnboardingService onboardingService,
        ICurrentUser currentUser,
        IValidator<OnboardingCompleteRequest> completeValidator)
    {
        _currencyCatalog = currencyCatalog;
        _onboardingService = onboardingService;
        _currentUser = currentUser;
        _completeValidator = completeValidator;
    }

    [HttpGet("currencies")]
    public IActionResult GetCurrencies()
    {
        return Ok(_currencyCatalog.GetCommonCurrencies());
    }

    // Tells the frontend which step of the wizard to show, if the user reloads the
    // page or comes back later without finishing.
    [HttpGet("state")]
    public async Task<ActionResult<OnboardingStateResult>> GetState(CancellationToken cancellationToken)
    {
        var result = await _onboardingService.GetStateAsync(_currentUser.UserId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<BudgetTemplateDto>>> GetTemplates(CancellationToken cancellationToken)
    {
        var result = await _onboardingService.GetTemplatesAsync(cancellationToken);
        return Ok(result);
    }

    // The last step: saves currency, timezone, categories and the first income split,
    // all together. Can only run once per account.
    [HttpPost("complete")]
    public async Task<ActionResult<AllocationVersionResult>> Complete(OnboardingCompleteRequest request, CancellationToken cancellationToken)
    {
        await _completeValidator.ValidateOrThrowAsync(request, cancellationToken);
        var result = await _onboardingService.CompleteAsync(_currentUser.UserId, request, cancellationToken);
        return Ok(result);
    }
}
