using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Reports;
using Natoshare.Domain.Budgeting;
using Natoshare.Infrastructure.Persistence;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Natoshare.Infrastructure.Reports;

// Turns the ledger's real numbers into the reports a user can read on screen or take
// away as a file. Nothing here writes anything, a report is always worked out fresh
// from CategoryMonth and DeficitResolution rows, never cached.
public class ReportService : IReportService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IEntitlementService _entitlementService;

    public ReportService(NatoshareDbContext dbContext, IEntitlementService entitlementService)
    {
        _dbContext = dbContext;
        _entitlementService = entitlementService;
    }

    public Task<MonthlyReportDto> GetMonthlyAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default) =>
        BuildMonthlyReportAsync(userId, year, month, cancellationToken);

    public async Task<RangeReportDto> GetRangeAsync(
        Guid userId, int fromYear, int fromMonth, int toYear, int toMonth, CancellationToken cancellationToken = default)
    {
        var months = new List<MonthlyReportDto>();
        foreach (var (year, month) in MonthsBetween(fromYear, fromMonth, toYear, toMonth))
        {
            months.Add(await BuildMonthlyReportAsync(userId, year, month, cancellationToken));
        }

        return new RangeReportDto(
            fromYear, fromMonth, toYear, toMonth, months,
            months.Sum(m => m.TotalBudget), months.Sum(m => m.TotalActual), months.Sum(m => m.TotalSaved),
            OverallSavingsRate(months.Sum(m => m.TotalBudget), months.Sum(m => m.TotalActual)));
    }

    public async Task<AnnualReportDto> GetAnnualAsync(Guid userId, int year, CancellationToken cancellationToken = default)
    {
        var months = new List<MonthlyReportDto>();
        for (var month = 1; month <= 12; month++)
        {
            months.Add(await BuildMonthlyReportAsync(userId, year, month, cancellationToken));
        }

        return new AnnualReportDto(
            year, months,
            months.Sum(m => m.TotalBudget), months.Sum(m => m.TotalActual), months.Sum(m => m.TotalSaved),
            OverallSavingsRate(months.Sum(m => m.TotalBudget), months.Sum(m => m.TotalActual)));
    }

    public async Task<ReportExportResult> ExportAsync(Guid userId, ExportReportRequest request, CancellationToken cancellationToken = default)
    {
        await _entitlementService.EnsureEntitledAsync(userId, "Report export", cancellationToken);
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        IReadOnlyList<MonthlyReportDto> months;
        string scopeLabel;

        if (request.Scope == "month")
        {
            months = [await GetMonthlyAsync(userId, request.Year!.Value, request.Month!.Value, cancellationToken)];
            scopeLabel = $"{request.Year}-{request.Month:D2}";
        }
        else if (request.Scope == "annual")
        {
            months = (await GetAnnualAsync(userId, request.Year!.Value, cancellationToken)).Months;
            scopeLabel = $"{request.Year}";
        }
        else
        {
            months = (await GetRangeAsync(
                userId, request.FromYear!.Value, request.FromMonth!.Value, request.ToYear!.Value, request.ToMonth!.Value, cancellationToken)).Months;
            scopeLabel = $"{request.FromYear}-{request.FromMonth:D2}_to_{request.ToYear}-{request.ToMonth:D2}";
        }

        var fileNameBase = $"natoshare-report-{request.Scope}-{scopeLabel}";

        return request.Format == "csv"
            ? new ReportExportResult(BuildCsv(months), "text/csv", $"{fileNameBase}.csv")
            : new ReportExportResult(BuildPdf(months, user.CurrencySymbol, request.Scope, scopeLabel), "application/pdf", $"{fileNameBase}.pdf");
    }

    private async Task<MonthlyReportDto> BuildMonthlyReportAsync(Guid userId, int year, int month, CancellationToken cancellationToken)
    {
        var budgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == year && m.Month == month, cancellationToken);

        if (budgetMonth is null)
        {
            // Nothing has happened in this month yet (or ever), an honest report for
            // it is simply empty, there is no allocation preview to replicate here.
            return new MonthlyReportDto(year, month, false, [], [], 0m, 0m, 0m, 0m);
        }

        var categories = await _dbContext.Categories.Where(c => c.UserId == userId).ToDictionaryAsync(c => c.Id, cancellationToken);

        var (prevYear, prevMonth) = PreviousMonth(year, month);
        var prevBudgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == prevYear && m.Month == prevMonth, cancellationToken);
        var prevSpentByCategory = prevBudgetMonth?.CategoryMonths.ToDictionary(
                cm => cm.CategoryId, cm => cm.SpentAmount.Amount + (cm.ExternalTransferAmount?.Amount ?? 0m))
            ?? new Dictionary<Guid, decimal>();

        var lines = new List<CategoryReportLineDto>();
        foreach (var categoryMonth in budgetMonth.CategoryMonths)
        {
            if (!categories.TryGetValue(categoryMonth.CategoryId, out var category))
            {
                continue;
            }

            // Budget is the plan for the month (what was allocated, plus savings
            // carried in, minus a deficit carried in), deliberately not Funded()'s
            // own number, which also folds in CoveredAmount and ExternalTransferAmount,
            // after-the-fact adjustments that would otherwise quietly erase the very
            // overspend the Deficits & coverage section exists to explain.
            var budget = Math.Max(0m, categoryMonth.AllocatedAmount.Amount + categoryMonth.CarriedInSavings.Amount - categoryMonth.CarriedInDeficit.Amount);

            // A FixedAccount category's real "spend" is the confirmed external
            // transfer, not SpentAmount (which stays 0 for it, expenses never post
            // against a FixedAccount category).
            var actual = categoryMonth.SpentAmount.Amount + (categoryMonth.ExternalTransferAmount?.Amount ?? 0m);
            var deficit = categoryMonth.Deficit().Amount;

            // Once the month is closed, SavedThisMonth is the real, frozen figure.
            // Before that, nothing has actually been saved yet, "what is left right
            // now" is only a projection, not a fact worth reporting as Saved.
            var saved = categoryMonth.SavedThisMonth?.Amount ?? 0m;

            var variance = budget - actual;
            var status = actual == 0m ? "Unused" : actual > budget ? "Over" : "Under";
            var savingsRate = budget > 0m ? Math.Max(0m, budget - actual) / budget : 0m;

            decimal? prevMonthDelta = prevBudgetMonth is null
                ? null
                : actual - prevSpentByCategory.GetValueOrDefault(categoryMonth.CategoryId);

            lines.Add(new CategoryReportLineDto(
                categoryMonth.CategoryId, category.Name, budget, actual, saved, deficit, variance, status, savingsRate, prevMonthDelta));
        }

        var deficitResolutions = await _dbContext.DeficitResolutions
            .Where(d => d.BudgetMonthId == budgetMonth.Id)
            .ToListAsync(cancellationToken);

        var deficitsAndCoverage = deficitResolutions
            .Select(d => new DeficitCoverageLineDto(
                d.CategoryId,
                categories.TryGetValue(d.CategoryId, out var c) ? c.Name : "Unknown",
                d.Amount.Amount,
                d.Method.ToString(),
                d.SourceCategoryId,
                d.Method == DeficitResolutionMethod.NextMonthAllocation))
            .ToList();

        var totalBudget = lines.Sum(l => l.Budget);
        var totalActual = lines.Sum(l => l.Actual);
        var totalSaved = lines.Sum(l => l.Saved);

        return new MonthlyReportDto(
            year, month, budgetMonth.Status == BudgetMonthStatus.Closed, lines, deficitsAndCoverage,
            totalBudget, totalActual, totalSaved, OverallSavingsRate(totalBudget, totalActual));
    }

    private static decimal OverallSavingsRate(decimal budget, decimal actual) => budget > 0m ? Math.Max(0m, budget - actual) / budget : 0m;

    private static IEnumerable<(int Year, int Month)> MonthsBetween(int fromYear, int fromMonth, int toYear, int toMonth)
    {
        var (year, month) = (fromYear, fromMonth);
        while (year < toYear || (year == toYear && month <= toMonth))
        {
            yield return (year, month);
            (year, month) = month == 12 ? (year + 1, 1) : (year, month + 1);
        }
    }

    private static (int Year, int Month) PreviousMonth(int year, int month) => month == 1 ? (year - 1, 12) : (year, month - 1);

    private static byte[] BuildCsv(IReadOnlyList<MonthlyReportDto> months)
    {
        using var memoryStream = new MemoryStream();

        // UTF-8 with a BOM, so Excel (which otherwise guesses the wrong encoding)
        // opens currency symbols like ₦ or € correctly instead of showing garbage.
        using (var writer = new StreamWriter(memoryStream, new System.Text.UTF8Encoding(true), leaveOpen: true))
        using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            csv.WriteField("Year");
            csv.WriteField("Month");
            csv.WriteField("Category");
            csv.WriteField("Budget");
            csv.WriteField("Actual");
            csv.WriteField("Saved");
            csv.WriteField("Deficit");
            csv.WriteField("Variance");
            csv.WriteField("Status");
            csv.WriteField("SavingsRate");
            csv.NextRecord();

            foreach (var month in months)
            {
                foreach (var line in month.Categories)
                {
                    csv.WriteField(month.Year);
                    csv.WriteField(month.Month);
                    csv.WriteField(line.CategoryName);
                    csv.WriteField(line.Budget);
                    csv.WriteField(line.Actual);
                    csv.WriteField(line.Saved);
                    csv.WriteField(line.Deficit);
                    csv.WriteField(line.Variance);
                    csv.WriteField(line.Status);
                    csv.WriteField(line.SavingsRate);
                    csv.NextRecord();
                }
            }
        }

        return memoryStream.ToArray();
    }

    private static byte[] BuildPdf(IReadOnlyList<MonthlyReportDto> months, string currencySymbol, string scope, string scopeLabel)
    {
        string Money(decimal amount) => $"{currencySymbol}{amount:N2}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Text($"Natoshare report — {scope} ({scopeLabel})").FontSize(16).Bold();

                page.Content().Column(column =>
                {
                    column.Spacing(14);

                    foreach (var month in months)
                    {
                        if (month.Categories.Count == 0)
                        {
                            continue;
                        }

                        column.Item().Text($"{month.Year}-{month.Month:D2}{(month.IsClosed ? "" : " (still open)")}").FontSize(13).Bold();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                foreach (var text in new[] { "Category", "Budget", "Actual", "Saved", "Variance", "Status" })
                                {
                                    header.Cell().Text(text).Bold();
                                }
                            });

                            foreach (var line in month.Categories)
                            {
                                table.Cell().Text(line.CategoryName);
                                table.Cell().Text(Money(line.Budget));
                                table.Cell().Text(Money(line.Actual));
                                table.Cell().Text(Money(line.Saved));
                                table.Cell().Text(Money(line.Variance));
                                table.Cell().Text(line.Status);
                            }
                        });

                        if (month.DeficitsAndCoverage.Count > 0)
                        {
                            column.Item().Text("Deficits & coverage").FontSize(11).Bold();
                            foreach (var coverage in month.DeficitsAndCoverage)
                            {
                                column.Item().Text(
                                    $"{coverage.CategoryName}: {Money(coverage.Amount)} via {coverage.Method}"
                                        + (coverage.CarriedForward ? " (carried to next month)" : ""));
                            }
                        }
                    }

                    column.Item().PaddingTop(10).Text(
                        $"Total budget {Money(months.Sum(m => m.TotalBudget))} · "
                            + $"actual {Money(months.Sum(m => m.TotalActual))} · saved {Money(months.Sum(m => m.TotalSaved))}").Bold();
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}
