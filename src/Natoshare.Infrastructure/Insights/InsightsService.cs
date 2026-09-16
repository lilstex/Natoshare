using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Insights;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Common;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Insights;

// The plain-English "how am I doing" view. Built on the same current-month snapshot
// and pacing math everything else uses, this just packages it differently.
public class InsightsService : IInsightsService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IBudgetMonthService _budgetMonthService;

    public InsightsService(NatoshareDbContext dbContext, IClock clock, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<SpendingSummaryResult> GetSpendingSummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var snapshot = await _budgetMonthService.GetSnapshotAsync(userId, today.Year, today.Month, cancellationToken);

        var totalSpent = snapshot.Categories.Sum(c => c.Spent.Amount);
        var totalFunded = snapshot.Categories.Sum(c => c.ToCategoryMonth().Funded().Amount);

        var topCategories = snapshot.Categories
            .Where(c => c.Spent.Amount > 0m)
            .OrderByDescending(c => c.Spent.Amount)
            .Take(3)
            .Select(c => new TopCategoryDto(c.CategoryId, c.Name, c.Spent.Amount))
            .ToList();

        var trend = await ComputeTrendAsync(userId, today, totalSpent, cancellationToken);
        var plainEnglish = BuildPlainEnglish(user.CurrencySymbol, totalSpent, totalFunded, topCategories);

        return new SpendingSummaryResult(plainEnglish, today.Year, today.Month, topCategories, trend);
    }

    public async Task<IReadOnlyList<PacingInsightDto>> GetPacingAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var snapshot = await _budgetMonthService.GetSnapshotAsync(userId, today.Year, today.Month, cancellationToken);

        return snapshot.Categories.Select(c =>
        {
            var pace = PacingCalculator.Compute(c.ToCategoryMonth(), today.Year, today.Month, today);
            return new PacingInsightDto(c.CategoryId, c.Name, pace.Projected, pace.Status, pace.SafeToSpendDaily);
        }).ToList();
    }

    // Compares this month's spend so far against last month's, so the summary can
    // say "up" or "down" instead of just a raw number.
    private async Task<SpendingTrendDto> ComputeTrendAsync(Guid userId, DateOnly today, decimal currentSpent, CancellationToken cancellationToken)
    {
        var previous = today.AddMonths(-1);
        var previousMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == previous.Year && m.Month == previous.Month, cancellationToken);

        if (previousMonth is null)
        {
            return new SpendingTrendDto("Flat", null);
        }

        var previousSpent = previousMonth.CategoryMonths.Sum(cm => cm.SpentAmount.Amount);
        if (previousSpent <= 0m)
        {
            return new SpendingTrendDto(currentSpent > 0m ? "Up" : "Flat", null);
        }

        var percentChange = Math.Round((currentSpent - previousSpent) / previousSpent * 100m, 1);
        var direction = percentChange > 1m ? "Up" : percentChange < -1m ? "Down" : "Flat";
        return new SpendingTrendDto(direction, percentChange);
    }

    private static string BuildPlainEnglish(string currencySymbol, decimal totalSpent, decimal totalFunded, List<TopCategoryDto> topCategories)
    {
        if (totalSpent <= 0m)
        {
            return "Nothing logged yet this month.";
        }

        var baseLine = $"You've spent {currencySymbol}{totalSpent:N2} of {currencySymbol}{totalFunded:N2} funded this month";
        return topCategories.Count > 0 ? $"{baseLine}, mostly on {topCategories[0].Name}." : $"{baseLine}.";
    }
}
