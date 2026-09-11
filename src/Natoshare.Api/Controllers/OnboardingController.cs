using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Application.Common;

namespace Natoshare.Api.Controllers;

// Endpoints that help the onboarding wizard (built fully in Phase 2). For now this
// only has the currency picker's options.
[Route("api/v1/onboarding")]
[Authorize(Policy = "RequireUser")]
public class OnboardingController : ApiControllerBase
{
    private readonly ICurrencyCatalog _currencyCatalog;

    public OnboardingController(ICurrencyCatalog currencyCatalog)
    {
        _currencyCatalog = currencyCatalog;
    }

    [HttpGet("currencies")]
    public IActionResult GetCurrencies()
    {
        return Ok(_currencyCatalog.GetCommonCurrencies());
    }
}
