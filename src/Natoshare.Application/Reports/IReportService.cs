namespace Natoshare.Application.Reports;

public interface IReportService
{
    Task<MonthlyReportDto> GetMonthlyAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default);

    Task<RangeReportDto> GetRangeAsync(
        Guid userId, int fromYear, int fromMonth, int toYear, int toMonth, CancellationToken cancellationToken = default);

    Task<AnnualReportDto> GetAnnualAsync(Guid userId, int year, CancellationToken cancellationToken = default);

    // Needs an active entitlement (Pro or trial), 💳 in docs/02-api-surface.md.
    Task<ReportExportResult> ExportAsync(Guid userId, ExportReportRequest request, CancellationToken cancellationToken = default);
}
