using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Natoshare.Api.Validation;
using Natoshare.Application.Common;
using Natoshare.Application.Reports;

namespace Natoshare.Api.Controllers;

// Reading a report is free on every plan, only taking it away as a file needs an
// entitlement, see docs/02-api-surface.md's "(export only)" note on this section.
[Route("api/v1/reports")]
[Authorize(Policy = "RequireUser")]
public class ReportsController : ApiControllerBase
{
    private readonly IReportService _reportService;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<ExportReportRequest> _exportValidator;

    public ReportsController(IReportService reportService, ICurrentUser currentUser, IValidator<ExportReportRequest> exportValidator)
    {
        _reportService = reportService;
        _currentUser = currentUser;
        _exportValidator = exportValidator;
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyReportDto>> GetMonthly(int year, int month, CancellationToken cancellationToken)
    {
        var result = await _reportService.GetMonthlyAsync(_currentUser.UserId, year, month, cancellationToken);
        return Ok(result);
    }

    [HttpGet("range")]
    public async Task<ActionResult<RangeReportDto>> GetRange(
        int fromYear, int fromMonth, int toYear, int toMonth, CancellationToken cancellationToken)
    {
        var result = await _reportService.GetRangeAsync(_currentUser.UserId, fromYear, fromMonth, toYear, toMonth, cancellationToken);
        return Ok(result);
    }

    [HttpGet("annual")]
    public async Task<ActionResult<AnnualReportDto>> GetAnnual(int year, CancellationToken cancellationToken)
    {
        var result = await _reportService.GetAnnualAsync(_currentUser.UserId, year, cancellationToken);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        string scope, string format, int? year, int? month, int? fromYear, int? fromMonth, int? toYear, int? toMonth,
        CancellationToken cancellationToken)
    {
        var request = new ExportReportRequest(scope, format, year, month, fromYear, fromMonth, toYear, toMonth);
        await _exportValidator.ValidateOrThrowAsync(request, cancellationToken);

        var result = await _reportService.ExportAsync(_currentUser.UserId, request, cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
