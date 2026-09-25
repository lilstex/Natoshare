using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.PeopleAndMoney;
using Natoshare.Domain.Planning;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// A calendar of upcoming dates the user should keep an eye on. Every type in the
// documented union is covered now that Phase 7 has built RecurringItem.
public class ObligationsService : IObligationsService
{
    // How far out a Free account can see, docs/00-plan.md section 5 only says
    // "basic" for Free's obligations calendar without a number, a week matches the
    // "next 7 days" slice the dashboard itself already shows everyone.
    private const int BasicPlanMaxDays = 7;

    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IEntitlementService _entitlementService;

    public ObligationsService(NatoshareDbContext dbContext, IClock clock, IEntitlementService entitlementService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _entitlementService = entitlementService;
    }

    public async Task<IReadOnlyList<ObligationItemDto>> ListAsync(Guid userId, int days, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);

        var entitlements = await _entitlementService.ResolveAsync(userId, cancellationToken);
        if (entitlements.Plan == "Free")
        {
            days = Math.Min(days, BasicPlanMaxDays);
        }

        var horizon = today.AddDays(days);

        var items = new List<ObligationItemDto>();

        var debts = await _dbContext.DebtsIn
            .Where(d => d.UserId == userId && d.Status != DebtInStatus.Repaid && d.DueOn != null)
            .ToListAsync(cancellationToken);
        foreach (var debt in debts.Where(d => d.DueOn!.Value <= horizon))
        {
            var overdue = debt.DueOn!.Value < today;
            items.Add(new ObligationItemDto(
                debt.DueOn!.Value, "DebtDue", $"Pay back {debt.LenderName}", debt.Amount.Amount, debt.Id,
                overdue ? "Critical" : "Warning"));
        }

        var loans = await _dbContext.LoansOut
            .Where(l => l.UserId == userId && l.Status != LoanOutStatus.Repaid && l.Status != LoanOutStatus.WrittenOff && l.ExpectedReturnOn != null)
            .ToListAsync(cancellationToken);
        foreach (var loan in loans.Where(l => l.ExpectedReturnOn!.Value <= horizon))
        {
            var overdue = loan.ExpectedReturnOn!.Value < today;
            items.Add(new ObligationItemDto(
                loan.ExpectedReturnOn!.Value, "LoanReturn", $"{loan.BorrowerName} was due to return this",
                loan.Amount.Amount, loan.Id, overdue ? "Critical" : "Warning"));
        }

        // A promise has no due date of its own, it stays on the calendar at "today"
        // for as long as it is still open, instead of disappearing off a fixed date.
        var openPromises = await _dbContext.Promises
            .Include(p => p.Redemptions)
            .Where(p => p.UserId == userId && (p.Status == PromiseStatus.Open || p.Status == PromiseStatus.PartiallyRedeemed))
            .ToListAsync(cancellationToken);
        items.AddRange(openPromises.Select(p => new ObligationItemDto(
            today, "PromiseReminder", $"You promised {p.PersonName}",
            Math.Max(0m, p.Amount.Amount - p.Redemptions.Sum(r => r.Amount.Amount)), p.Id, "Info")));

        var currentMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == today.Year && m.Month == today.Month, cancellationToken);

        if (currentMonth is not null)
        {
            var categories = await _dbContext.Categories
                .Where(c => c.UserId == userId && c.Kind == CategoryKind.FixedAccount)
                .ToDictionaryAsync(c => c.Id, cancellationToken);
            var lastDayOfMonth = new DateOnly(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));

            // Unlike an overdue debt or loan (always in the past, so always inside
            // any forward-looking window by definition), this one sits at the end
            // of the current month, a genuinely future date that has to be checked
            // against the horizon like any other upcoming item.
            if (lastDayOfMonth <= horizon)
            {
                foreach (var categoryMonth in currentMonth.CategoryMonths.Where(cm => !cm.ExternalTransferConfirmed))
                {
                    if (categories.TryGetValue(categoryMonth.CategoryId, out var category))
                    {
                        items.Add(new ObligationItemDto(
                            lastDayOfMonth, "FixedAccountConfirm", $"Confirm {category.Name} was sent out",
                            categoryMonth.Funded().Amount, categoryMonth.CategoryId, "Warning"));
                    }
                }
            }

            foreach (var categoryMonth in currentMonth.CategoryMonths.Where(cm => cm.CarriedInDeficit.Amount > 0m))
            {
                if (categories.TryGetValue(categoryMonth.CategoryId, out var category))
                {
                    items.Add(new ObligationItemDto(
                        today, "CarriedDeficit", $"{category.Name} is carrying a deficit from last month",
                        categoryMonth.CarriedInDeficit.Amount, categoryMonth.CategoryId, "Warning"));
                }
            }
        }

        var dueRecurringItems = await _dbContext.RecurringItems
            .Where(r => r.UserId == userId && r.IsActive && r.NextRunOn <= horizon)
            .ToListAsync(cancellationToken);
        items.AddRange(dueRecurringItems.Select(r => new ObligationItemDto(
            r.NextRunOn, "RecurringItem", $"{r.Description} is due", r.Amount.Amount, r.Id, r.NextRunOn < today ? "Critical" : "Info")));

        var openOldMonths = await _dbContext.BudgetMonths
            .Where(m => m.UserId == userId && m.Status == BudgetMonthStatus.Open && (m.Year < today.Year || (m.Year == today.Year && m.Month < today.Month)))
            .ToListAsync(cancellationToken);
        items.AddRange(openOldMonths.Select(m => new ObligationItemDto(
            new DateOnly(m.Year, m.Month, DateTime.DaysInMonth(m.Year, m.Month)), "MonthClose",
            $"{m.Year}-{m.Month:D2} is still open and needs closing", null, m.Id, "Critical")));

        return items.OrderBy(i => i.Date).ToList();
    }
}
