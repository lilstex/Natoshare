using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Domain.Common;
using Natoshare.Domain.PeopleAndMoney;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// A lightweight cash-out record, separate from the Investment category's own ledger
// movement. "Allocated" is what the Investment category was funded with this month,
// "Invested" is what was actually logged as put to work, the gap between the two is
// the shortfall.
public class InvestmentLogService : IInvestmentLogService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IBudgetMonthService _budgetMonthService;

    public InvestmentLogService(NatoshareDbContext dbContext, IClock clock, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<IReadOnlyList<InvestmentLogDto>> ListAsync(
        Guid userId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.InvestmentLogs.Where(i => i.UserId == userId);

        if (from is not null)
        {
            query = query.Where(i => i.InvestedOn >= from);
        }

        if (to is not null)
        {
            query = query.Where(i => i.InvestedOn <= to);
        }

        var logs = await query.OrderByDescending(i => i.InvestedOn).ThenByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        return logs.Select(ToDto).ToList();
    }

    public async Task<InvestmentLogDto> CreateAsync(Guid userId, CreateInvestmentLogRequest request, CancellationToken cancellationToken = default)
    {
        var budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, request.InvestedOn.Year, request.InvestedOn.Month, cancellationToken);

        var log = new InvestmentLog
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Amount = new Money(request.Amount),
            InvestedOn = request.InvestedOn,
            Platform = request.Platform,
            Note = request.Note,
            BudgetMonthId = budgetMonthId,
            CreatedAt = _clock.UtcNow,
        };

        _dbContext.InvestmentLogs.Add(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(log);
    }

    public async Task<InvestmentSummaryDto> GetSummaryAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var snapshot = await _budgetMonthService.GetSnapshotAsync(userId, year, month, cancellationToken);

        // "Investment" is the name Natoshare seeds by default (see DefaultCategories),
        // matched by name since there is no dedicated CategoryKind for it, a user is
        // free to rename or even remove it, in which case nothing is allocated yet.
        var allocated = snapshot.Categories
            .Where(c => string.Equals(c.Name, "Investment", StringComparison.OrdinalIgnoreCase))
            .Sum(c => c.ToCategoryMonth().Funded().Amount);

        var logs = await _dbContext.InvestmentLogs
            .Where(i => i.UserId == userId && i.InvestedOn.Year == year && i.InvestedOn.Month == month)
            .ToListAsync(cancellationToken);

        var invested = logs.Sum(i => i.Amount.Amount);
        var byPlatform = logs
            .GroupBy(i => i.Platform)
            .Select(g => new InvestmentByPlatformDto(g.Key, g.Sum(i => i.Amount.Amount)))
            .OrderByDescending(p => p.Amount)
            .ToList();

        return new InvestmentSummaryDto(allocated, invested, Math.Max(0m, allocated - invested), byPlatform);
    }

    public async Task DeleteAsync(Guid userId, Guid investmentId, CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.InvestmentLogs.FirstOrDefaultAsync(i => i.Id == investmentId && i.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that investment log entry.");

        _dbContext.InvestmentLogs.Remove(log);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static InvestmentLogDto ToDto(InvestmentLog log) =>
        new(log.Id, log.Amount.Amount, log.InvestedOn, log.Platform, log.Note, log.CreatedAt);
}
