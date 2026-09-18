namespace Natoshare.Application.Reports;

// "Over" means actual spend crossed the budget, "Unused" means nothing was spent at
// all, "Under" is everything else.
public record CategoryReportLineDto(
    Guid CategoryId,
    string CategoryName,
    decimal Budget,
    decimal Actual,
    decimal Saved,
    decimal Deficit,
    decimal Variance,
    string Status,
    decimal SavingsRate,
    decimal? PrevMonthDelta);

public record DeficitCoverageLineDto(Guid CategoryId, string CategoryName, decimal Amount, string Method, Guid? SourceCategoryId, bool CarriedForward);

public record MonthlyReportDto(
    int Year,
    int Month,
    bool IsClosed,
    IReadOnlyList<CategoryReportLineDto> Categories,
    IReadOnlyList<DeficitCoverageLineDto> DeficitsAndCoverage,
    decimal TotalBudget,
    decimal TotalActual,
    decimal TotalSaved,
    decimal OverallSavingsRate);

public record RangeReportDto(
    int FromYear,
    int FromMonth,
    int ToYear,
    int ToMonth,
    IReadOnlyList<MonthlyReportDto> Months,
    decimal TotalBudget,
    decimal TotalActual,
    decimal TotalSaved,
    decimal OverallSavingsRate);

public record AnnualReportDto(
    int Year,
    IReadOnlyList<MonthlyReportDto> Months,
    decimal TotalBudget,
    decimal TotalActual,
    decimal TotalSaved,
    decimal OverallSavingsRate);

public record ExportReportRequest(
    string Scope,
    string Format,
    int? Year,
    int? Month,
    int? FromYear,
    int? FromMonth,
    int? ToYear,
    int? ToMonth);

public record ReportExportResult(byte[] Content, string ContentType, string FileName);
