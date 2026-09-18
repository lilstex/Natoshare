using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Dashboard;
using Natoshare.Application.Insights;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Dashboard;

// The one-call version of the dashboard: everything the hero screen needs, so the
// page does not have to fan out to eight endpoints itself on first load. Every
// figure here also has its own dedicated endpoint for anything that only needs one
// piece of this.
public class DashboardService : IDashboardService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IBalanceService _balanceService;
    private readonly INetPositionService _netPositionService;
    private readonly IInsightsService _insightsService;
    private readonly IObligationsService _obligationsService;
    private readonly IPromiseService _promiseService;
    private readonly IIncomeService _incomeService;
    private readonly IExpenseService _expenseService;

    public DashboardService(
        NatoshareDbContext dbContext,
        IBalanceService balanceService,
        INetPositionService netPositionService,
        IInsightsService insightsService,
        IObligationsService obligationsService,
        IPromiseService promiseService,
        IIncomeService incomeService,
        IExpenseService expenseService)
    {
        _dbContext = dbContext;
        _balanceService = balanceService;
        _netPositionService = netPositionService;
        _insightsService = insightsService;
        _obligationsService = obligationsService;
        _promiseService = promiseService;
        _incomeService = incomeService;
        _expenseService = expenseService;
    }

    public async Task<DashboardDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var balances = await _balanceService.GetCurrentAsync(userId, cancellationToken);
        var netPosition = await _netPositionService.GetAsync(userId, cancellationToken);
        var pacing = await _insightsService.GetPacingAsync(userId, cancellationToken);
        var obligations = await _obligationsService.ListAsync(userId, 7, cancellationToken);

        var promises = await _promiseService.ListAsync(userId, null, cancellationToken);
        var openPromises = promises
            .Where(p => p.Status is "Open" or "PartiallyRedeemed")
            .Select(p => new OpenPromiseSummaryDto(p.Id, p.PersonName, p.Outstanding))
            .ToList();

        var incomes = await _incomeService.ListAsync(userId, null, null, null, 1, 10, cancellationToken);
        var expenses = await _expenseService.ListAsync(userId, null, null, null, null, null, 1, 10, cancellationToken);
        var recentTransactions = incomes
            .Select(i => new RecentTransactionDto(i.Id, i.Description, i.Amount, true, i.OccurredOn))
            .Concat(expenses.Select(e => new RecentTransactionDto(e.Id, e.Description, e.Amount, false, e.OccurredOn)))
            .OrderByDescending(t => t.OccurredOn)
            .Take(10)
            .ToList();

        var unreadAlerts = await _dbContext.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

        // Deficit cards matter most, they sort first (see docs/06-design-system.md's
        // dashboard screen pattern), everything else keeps its original order.
        var categoryCards = balances.Categories.OrderByDescending(c => c.Deficit).ToList();

        return new DashboardDto(
            netPosition,
            balances.Month,
            categoryCards,
            pacing,
            balances.FlexiblePool,
            obligations,
            openPromises,
            netPosition.Breakdown.LoansOut,
            netPosition.Breakdown.DebtsIn,
            recentTransactions,
            unreadAlerts);
    }
}
