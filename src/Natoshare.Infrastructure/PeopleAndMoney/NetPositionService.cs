using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.PeopleAndMoney;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// The one-number answer to "where do I really stand": everything saved or already
// deployed, plus what other people still owe the user, minus what the user still
// owes back, minus deficits still hanging over the current month. See
// docs/01-domain-model.md section 6 for the exact formula this follows.
public class NetPositionService : INetPositionService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;

    public NetPositionService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<NetPositionDto> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var snapshot = await _budgetMonthService.GetSnapshotAsync(userId, today.Year, today.Month, cancellationToken);

        var savings = 0m;
        var deployed = 0m;
        var carriedDeficits = 0m;

        foreach (var category in snapshot.Categories)
        {
            savings += (await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.CategorySavings(category.CategoryId), cancellationToken)).Amount;
            deployed += (await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.External(category.CategoryId), cancellationToken)).Amount;
            carriedDeficits += category.CarriedInDeficit.Amount;
        }

        var pool = (await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.FlexiblePool(), cancellationToken)).Amount;

        var loansOut = await _dbContext.LoansOut
            .Include(l => l.Repayments)
            .Where(l => l.UserId == userId && (l.Status == LoanOutStatus.Outstanding || l.Status == LoanOutStatus.PartiallyRepaid))
            .ToListAsync(cancellationToken);
        var loansOutTotal = loansOut.Sum(l => Math.Max(0m, l.Amount.Amount - l.Repayments.Sum(r => r.Amount.Amount)));

        var debtsIn = await _dbContext.DebtsIn
            .Include(d => d.Repayments)
            .Where(d => d.UserId == userId && (d.Status == DebtInStatus.Outstanding || d.Status == DebtInStatus.PartiallyRepaid))
            .ToListAsync(cancellationToken);
        var debtsInTotal = debtsIn.Sum(d => Math.Max(0m, d.Amount.Amount - d.Repayments.Sum(r => r.Amount.Amount)));

        var openPromises = await _dbContext.Promises
            .Include(p => p.Redemptions)
            .Where(p => p.UserId == userId && (p.Status == PromiseStatus.Open || p.Status == PromiseStatus.PartiallyRedeemed))
            .ToListAsync(cancellationToken);
        var openPromisesTotal = openPromises.Sum(p => Math.Max(0m, p.Amount.Amount - p.Redemptions.Sum(r => r.Amount.Amount)));

        var total = savings + deployed + pool + loansOutTotal - debtsInTotal - openPromisesTotal - carriedDeficits;

        return new NetPositionDto(
            total, new NetPositionBreakdownDto(savings, deployed, pool, loansOutTotal, debtsInTotal, openPromisesTotal, carriedDeficits));
    }
}
