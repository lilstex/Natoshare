using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.PeopleAndMoney;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// Money the user borrowed from someone else. Borrowing itself never touches the
// ledger, most borrowed cash never actually lands in one of the user's tracked
// accounts, only paying it back does, and only when the user links a source for it.
public class DebtInService : IDebtInService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;

    public DebtInService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<IReadOnlyList<DebtInDto>> ListAsync(Guid userId, string? status, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.DebtsIn.Include(d => d.Repayments).Where(d => d.UserId == userId);

        if (status is not null)
        {
            query = query.Where(d => d.Status.ToString() == status);
        }

        var debts = await query.OrderByDescending(d => d.BorrowedOn).ThenByDescending(d => d.CreatedAt).ToListAsync(cancellationToken);
        return debts.Select(ToDto).ToList();
    }

    public async Task<DebtInDto> CreateAsync(Guid userId, CreateDebtInRequest request, CancellationToken cancellationToken = default)
    {
        var debt = new DebtIn
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            LenderName = request.LenderName,
            Amount = new Money(request.Amount),
            BorrowedOn = request.BorrowedOn,
            DueOn = request.DueOn,
            Note = request.Note,
            CreatedAt = _clock.UtcNow,
        };

        _dbContext.DebtsIn.Add(debt);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(debt);
    }

    public async Task<DebtInDto> AddRepaymentAsync(
        Guid userId, Guid debtId, CreateDebtRepaymentRequest request, CancellationToken cancellationToken = default)
    {
        var debt = await LoadOwnedAsync(userId, debtId, cancellationToken);
        if (debt.Status == DebtInStatus.Repaid)
        {
            throw new ConflictException("This debt is already fully repaid.");
        }

        var amount = new Money(request.Amount);
        var repayment = new DebtRepayment
        {
            Id = Guid.CreateVersion7(),
            DebtInId = debt.Id,
            Amount = amount,
            PaidOn = request.PaidOn,
            Note = request.Note,
            CreatedAt = _clock.UtcNow,
        };

        Guid? budgetMonthId = null;
        if (request.LinkedSource is not null)
        {
            var source = AccountRefMapping.ToAccountRef(request.LinkedSource);
            var available = await _ledgerService.GetAccountBalanceAsync(userId, source, cancellationToken);
            if (available < amount)
            {
                throw new ConflictException("That account does not have enough to pay this back with.");
            }

            repayment.LinkedSourceKind = source.Kind;
            repayment.LinkedSourceCategoryId = source.CategoryId;

            budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, request.PaidOn.Year, request.PaidOn.Month, cancellationToken);
            await CategoryMonthCacheHelper.AdjustAllocatedAsync(_dbContext, budgetMonthId.Value, source, amount, isCredit: false, cancellationToken);
        }

        // Added to the DbSet directly, not to debt.Repayments: the debt here was
        // loaded from the database (not new), so EF Core cannot tell a brand new
        // child with an already-assigned Guid key apart from an existing one it
        // should just update, unless we say explicitly that this one is new. EF's own
        // relationship fixup then adds it into debt.Repayments for us, adding it there
        // by hand too would just duplicate it in that list.
        _dbContext.DebtRepayments.Add(repayment);

        var totalRepaid = debt.Repayments.Sum(r => r.Amount.Amount);
        debt.Status = ObligationStatusCalculator.ComputeDebtStatus(debt.Amount.Amount, totalRepaid);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.LinkedSource is not null)
        {
            var source = AccountRefMapping.ToAccountRef(request.LinkedSource);
            await _ledgerService.PostAsync(
                userId, budgetMonthId, source, LedgerEntryType.LoanLink, amount, LedgerDirection.Debit,
                SourceTxnType.DebtRepayment, repayment.Id, request.Note, cancellationToken);
        }

        return ToDto(debt);
    }

    public async Task<DebtInDto> UpdateAsync(
        Guid userId, Guid debtId, UpdateDebtInRequest request, CancellationToken cancellationToken = default)
    {
        var debt = await LoadOwnedAsync(userId, debtId, cancellationToken);

        debt.LenderName = request.LenderName ?? debt.LenderName;
        debt.DueOn = request.DueOn ?? debt.DueOn;
        debt.Note = request.Note ?? debt.Note;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(debt);
    }

    public async Task DeleteAsync(Guid userId, Guid debtId, CancellationToken cancellationToken = default)
    {
        var debt = await LoadOwnedAsync(userId, debtId, cancellationToken);
        if (debt.Repayments.Count > 0)
        {
            throw new ConflictException("This debt already has repayments recorded, it cannot be deleted any more.");
        }

        _dbContext.DebtsIn.Remove(debt);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DebtIn> LoadOwnedAsync(Guid userId, Guid debtId, CancellationToken cancellationToken)
    {
        return await _dbContext.DebtsIn.Include(d => d.Repayments)
            .FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that debt.");
    }

    private static DebtInDto ToDto(DebtIn debt)
    {
        var totalRepaid = debt.Repayments.Sum(r => r.Amount.Amount);
        var outstanding = Math.Max(0m, debt.Amount.Amount - totalRepaid);

        return new DebtInDto(
            debt.Id, debt.LenderName, debt.Amount.Amount, debt.BorrowedOn, debt.DueOn, debt.Note,
            debt.Status.ToString(), totalRepaid, outstanding, debt.CreatedAt,
            debt.Repayments.OrderBy(r => r.PaidOn).Select(r => new DebtRepaymentDto(
                r.Id, r.Amount.Amount, r.PaidOn, r.Note,
                AccountRefMapping.ToInput(r.LinkedSourceKind, r.LinkedSourceCategoryId), r.CreatedAt)).ToList());
    }
}
