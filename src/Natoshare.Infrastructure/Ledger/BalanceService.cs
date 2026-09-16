using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Domain.Ledger;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// The read models every screen in Natoshare actually looks at: what each category
// has, what is safe to spend, and what the Flexible Pool is holding. Nothing here is
// stored on its own, it is all worked out from the ledger and the current month's
// snapshot.
public class BalanceService : IBalanceService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;

    public BalanceService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<BalancesResult> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var snapshot = await _budgetMonthService.GetSnapshotAsync(userId, today.Year, today.Month, cancellationToken);

        var categories = new List<CategoryBalanceDto>();
        foreach (var c in snapshot.Categories)
        {
            var categoryMonth = c.ToCategoryMonth();

            var savingsBalance = await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.CategorySavings(c.CategoryId), cancellationToken);
            var deployedBalance = await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.External(c.CategoryId), cancellationToken);
            var pace = PacingCalculator.Compute(categoryMonth, today.Year, today.Month, today);

            categories.Add(new CategoryBalanceDto(
                c.CategoryId,
                c.Name,
                c.Kind,
                c.Allocated.Amount,
                c.CarriedInSavings.Amount,
                c.CarriedInDeficit.Amount,
                c.Covered.Amount,
                categoryMonth.Funded().Amount,
                c.Spent.Amount,
                categoryMonth.Available().Amount,
                categoryMonth.Deficit().Amount,
                savingsBalance.Amount,
                deployedBalance.Amount,
                new PaceDto(pace.Projected, pace.Status),
                new SafeToSpendDto(pace.SafeToSpendDaily, pace.SafeToSpendDaily * 7)));
        }

        var poolBalance = await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.FlexiblePool(), cancellationToken);

        // "Saved to date" only ever grows once a month actually closes and rolls
        // surplus into savings (Phase 5), until then there is nothing saved yet.
        var savedToDate = 0m;

        var totals = new TotalsDto(
            categories.Sum(c => c.Allocated),
            categories.Sum(c => c.Spent),
            categories.Sum(c => c.Available),
            categories.Sum(c => c.Deficit),
            savedToDate);

        return new BalancesResult(
            new MonthSummaryDto(today.Year, today.Month, snapshot.Status), categories, new FlexiblePoolDto(poolBalance.Amount), totals);
    }

    public async Task<IReadOnlyList<BalanceHistoryItemDto>> GetHistoryAsync(
        Guid userId, Guid? categoryId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.BudgetMonths.Include(m => m.CategoryMonths).Where(m => m.UserId == userId);

        if (from is not null)
        {
            query = query.Where(m => m.Year > from.Value.Year || (m.Year == from.Value.Year && m.Month >= from.Value.Month));
        }

        if (to is not null)
        {
            query = query.Where(m => m.Year < to.Value.Year || (m.Year == to.Value.Year && m.Month <= to.Value.Month));
        }

        var months = await query.OrderBy(m => m.Year).ThenBy(m => m.Month).ToListAsync(cancellationToken);

        var results = new List<BalanceHistoryItemDto>();
        foreach (var month in months)
        {
            var categoryMonths = categoryId is null
                ? month.CategoryMonths
                : month.CategoryMonths.Where(cm => cm.CategoryId == categoryId).ToList();

            if (categoryId is not null && categoryMonths.Count == 0)
            {
                continue;
            }

            var allocated = categoryMonths.Sum(cm => cm.AllocatedAmount.Amount);
            var spent = categoryMonths.Sum(cm => cm.SpentAmount.Amount);
            var saved = categoryMonths.Sum(cm => cm.SavedThisMonth?.Amount ?? 0m);
            var deficit = categoryMonths.Sum(cm => cm.Deficit().Amount);

            results.Add(new BalanceHistoryItemDto(month.Year, month.Month, allocated, spent, saved, deficit));
        }

        return results;
    }
}
