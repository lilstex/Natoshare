using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.PeopleAndMoney;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// Money the user lent out. Lending it and getting it back are each their own ledger
// action (LoanDisbursement, LoanRepayment), only posted at all when the user chooses
// to link one of their own accounts, tracking a loan never requires that.
public class LoanOutService : ILoanOutService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;

    public LoanOutService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<IReadOnlyList<LoanOutDto>> ListAsync(Guid userId, string? status, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.LoansOut.Include(l => l.Repayments).Where(l => l.UserId == userId);

        if (status is not null)
        {
            query = query.Where(l => l.Status.ToString() == status);
        }

        var loans = await query.OrderByDescending(l => l.LentOn).ThenByDescending(l => l.CreatedAt).ToListAsync(cancellationToken);
        return loans.Select(ToDto).ToList();
    }

    public async Task<LoanOutDto> CreateAsync(Guid userId, CreateLoanOutRequest request, CancellationToken cancellationToken = default)
    {
        var amount = new Money(request.Amount);
        var loan = new LoanOut
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            BorrowerName = request.BorrowerName,
            Amount = amount,
            LentOn = request.LentOn,
            ExpectedReturnOn = request.ExpectedReturnOn,
            Note = request.Note,
            CreatedAt = _clock.UtcNow,
        };

        if (request.LinkedSource is not null)
        {
            var source = AccountRefMapping.ToAccountRef(request.LinkedSource);
            var available = await _ledgerService.GetAccountBalanceAsync(userId, source, cancellationToken);
            if (available < amount)
            {
                throw new ConflictException("That account does not have enough to lend out.");
            }

            loan.LinkedSourceKind = source.Kind;
            loan.LinkedSourceCategoryId = source.CategoryId;

            var budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, request.LentOn.Year, request.LentOn.Month, cancellationToken);
            await CategoryMonthCacheHelper.AdjustAllocatedAsync(_dbContext, budgetMonthId, source, amount, isCredit: false, cancellationToken);
            _dbContext.LoansOut.Add(loan);
            await _dbContext.SaveChangesAsync(cancellationToken);

            await _ledgerService.PostAsync(
                userId, budgetMonthId, source, LedgerEntryType.LoanLink, amount, LedgerDirection.Debit,
                SourceTxnType.LoanDisbursement, loan.Id, request.Note, cancellationToken);
        }
        else
        {
            _dbContext.LoansOut.Add(loan);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ToDto(loan);
    }

    public async Task<LoanOutDto> AddRepaymentAsync(
        Guid userId, Guid loanId, CreateLoanRepaymentRequest request, CancellationToken cancellationToken = default)
    {
        var loan = await LoadOwnedAsync(userId, loanId, cancellationToken);
        if (loan.Status is LoanOutStatus.Repaid or LoanOutStatus.WrittenOff)
        {
            throw new ConflictException("This loan is already settled.");
        }

        var amount = new Money(request.Amount);
        var repayment = new LoanRepayment
        {
            Id = Guid.CreateVersion7(),
            LoanOutId = loan.Id,
            Amount = amount,
            ReceivedOn = request.ReceivedOn,
            Note = request.Note,
            CreatedAt = _clock.UtcNow,
        };

        Guid? budgetMonthId = null;
        if (request.LinkedDestination is not null)
        {
            var destination = AccountRefMapping.ToAccountRef(request.LinkedDestination);
            repayment.LinkedDestinationKind = destination.Kind;
            repayment.LinkedDestinationCategoryId = destination.CategoryId;

            budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, request.ReceivedOn.Year, request.ReceivedOn.Month, cancellationToken);
            await CategoryMonthCacheHelper.AdjustAllocatedAsync(_dbContext, budgetMonthId.Value, destination, amount, isCredit: true, cancellationToken);
        }

        // Added to the DbSet directly, not to loan.Repayments: the loan here was
        // loaded from the database (not new), so EF Core cannot tell a brand new
        // child with an already-assigned Guid key apart from an existing one it
        // should just update, unless we say explicitly that this one is new. EF's own
        // relationship fixup then adds it into loan.Repayments for us, adding it there
        // by hand too would just duplicate it in that list.
        _dbContext.LoanRepayments.Add(repayment);

        var totalRepaid = loan.Repayments.Sum(r => r.Amount.Amount);
        loan.Status = ObligationStatusCalculator.ComputeLoanStatus(loan.Amount.Amount, totalRepaid, loan.Status);

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (request.LinkedDestination is not null)
        {
            var destination = AccountRefMapping.ToAccountRef(request.LinkedDestination);
            await _ledgerService.PostAsync(
                userId, budgetMonthId, destination, LedgerEntryType.LoanLink, amount, LedgerDirection.Credit,
                SourceTxnType.LoanRepayment, repayment.Id, request.Note, cancellationToken);
        }

        return ToDto(loan);
    }

    public async Task<LoanOutDto> UpdateAsync(
        Guid userId, Guid loanId, UpdateLoanOutRequest request, CancellationToken cancellationToken = default)
    {
        var loan = await LoadOwnedAsync(userId, loanId, cancellationToken);

        if (request.Status == "WrittenOff")
        {
            if (loan.Status == LoanOutStatus.Repaid)
            {
                throw new ConflictException("This loan is already fully repaid, there is nothing left to write off.");
            }

            loan.Status = LoanOutStatus.WrittenOff;
        }

        loan.BorrowerName = request.BorrowerName ?? loan.BorrowerName;
        loan.ExpectedReturnOn = request.ExpectedReturnOn ?? loan.ExpectedReturnOn;
        loan.Note = request.Note ?? loan.Note;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(loan);
    }

    public async Task DeleteAsync(Guid userId, Guid loanId, CancellationToken cancellationToken = default)
    {
        var loan = await LoadOwnedAsync(userId, loanId, cancellationToken);
        if (loan.Repayments.Count > 0)
        {
            throw new ConflictException("This loan already has repayments recorded, write it off instead of deleting it.");
        }

        if (loan.LinkedSourceKind is not null)
        {
            var source = loan.LinkedSourceKind == AccountKind.FlexiblePool
                ? AccountRef.FlexiblePool()
                : loan.LinkedSourceKind == AccountKind.CategorySavings
                    ? AccountRef.CategorySavings(loan.LinkedSourceCategoryId!.Value)
                    : AccountRef.Category(loan.LinkedSourceCategoryId!.Value);

            await _ledgerService.ReverseAsync(SourceTxnType.LoanDisbursement, loan.Id, "Deleted", cancellationToken);

            var budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, loan.LentOn.Year, loan.LentOn.Month, cancellationToken);
            await CategoryMonthCacheHelper.AdjustAllocatedAsync(_dbContext, budgetMonthId, source, loan.Amount, isCredit: true, cancellationToken);
        }

        _dbContext.LoansOut.Remove(loan);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<LoanOut> LoadOwnedAsync(Guid userId, Guid loanId, CancellationToken cancellationToken)
    {
        return await _dbContext.LoansOut.Include(l => l.Repayments)
            .FirstOrDefaultAsync(l => l.Id == loanId && l.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that loan.");
    }

    private static LoanOutDto ToDto(LoanOut loan)
    {
        var totalRepaid = loan.Repayments.Sum(r => r.Amount.Amount);
        var outstanding = Math.Max(0m, loan.Amount.Amount - totalRepaid);

        return new LoanOutDto(
            loan.Id, loan.BorrowerName, loan.Amount.Amount, loan.LentOn, loan.ExpectedReturnOn, loan.Note,
            loan.Status.ToString(), AccountRefMapping.ToInput(loan.LinkedSourceKind, loan.LinkedSourceCategoryId),
            totalRepaid, outstanding, loan.CreatedAt,
            loan.Repayments.OrderBy(r => r.ReceivedOn).Select(r => new LoanRepaymentDto(
                r.Id, r.Amount.Amount, r.ReceivedOn, r.Note,
                AccountRefMapping.ToInput(r.LinkedDestinationKind, r.LinkedDestinationCategoryId), r.CreatedAt)).ToList());
    }
}
