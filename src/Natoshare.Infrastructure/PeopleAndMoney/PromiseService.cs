using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.PeopleAndMoney;
using Natoshare.Infrastructure.Persistence;
using DomainPromise = Natoshare.Domain.PeopleAndMoney.Promise;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// Money the user has promised someone but not handed over yet. Making the promise
// never touches the ledger, only redeeming it does, always straight out of a real
// account (a promise, unlike a loan or a debt, always needs one).
public class PromiseService : IPromiseService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;

    public PromiseService(NatoshareDbContext dbContext, IClock clock, ILedgerService ledgerService, IBudgetMonthService budgetMonthService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
    }

    public async Task<IReadOnlyList<PromiseDto>> ListAsync(Guid userId, string? status, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Promises.Include(p => p.Redemptions).Where(p => p.UserId == userId);

        if (status is not null)
        {
            query = query.Where(p => p.Status.ToString() == status);
        }

        var promises = await query.OrderByDescending(p => p.MadeOn).ThenByDescending(p => p.CreatedAt).ToListAsync(cancellationToken);
        return promises.Select(ToDto).ToList();
    }

    public async Task<PromiseDto> CreateAsync(Guid userId, CreatePromiseRequest request, CancellationToken cancellationToken = default)
    {
        var promise = new DomainPromise
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            PersonName = request.PersonName,
            Amount = new Money(request.Amount),
            Note = request.Note,
            MadeOn = request.MadeOn,
            CreatedAt = _clock.UtcNow,
        };

        _dbContext.Promises.Add(promise);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(promise);
    }

    public async Task<PromiseDto> AddRedemptionAsync(
        Guid userId, Guid promiseId, CreatePromiseRedemptionRequest request, CancellationToken cancellationToken = default)
    {
        var promise = await LoadOwnedAsync(userId, promiseId, cancellationToken);
        if (promise.Status is PromiseStatus.Redeemed or PromiseStatus.Cancelled)
        {
            throw new ConflictException("This promise is already settled.");
        }

        var amount = new Money(request.Amount);
        var source = AccountRefMapping.ToAccountRef(request.SourceAccount);
        var available = await _ledgerService.GetAccountBalanceAsync(userId, source, cancellationToken);
        if (available < amount)
        {
            throw new ConflictException("That account does not have enough to redeem this promise with.");
        }

        var redemption = new PromiseRedemption
        {
            Id = Guid.CreateVersion7(),
            PromiseId = promise.Id,
            Amount = amount,
            RedeemedOn = request.RedeemedOn,
            SourceAccountKind = source.Kind,
            SourceAccountCategoryId = source.CategoryId,
            Note = request.Note,
            CreatedAt = _clock.UtcNow,
        };

        // Added to the DbSet directly, not to promise.Redemptions: the promise here
        // was loaded from the database (not new), so EF Core cannot tell a brand new
        // child with an already-assigned Guid key apart from an existing one it
        // should just update, unless we say explicitly that this one is new. EF's own
        // relationship fixup then adds it into promise.Redemptions for us, adding it
        // there by hand too would just duplicate it in that list.
        _dbContext.PromiseRedemptions.Add(redemption);

        var totalRedeemed = promise.Redemptions.Sum(r => r.Amount.Amount);
        promise.Status = ObligationStatusCalculator.ComputePromiseStatus(promise.Amount.Amount, totalRedeemed, promise.Status);

        var budgetMonthId = await _budgetMonthService.EnsureOpenAsync(userId, request.RedeemedOn.Year, request.RedeemedOn.Month, cancellationToken);
        await CategoryMonthCacheHelper.AdjustAllocatedAsync(_dbContext, budgetMonthId, source, amount, isCredit: false, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _ledgerService.PostAsync(
            userId, budgetMonthId, source, LedgerEntryType.PromiseRedemption, amount, LedgerDirection.Debit,
            SourceTxnType.PromiseRedemption, redemption.Id, request.Note, cancellationToken);

        return ToDto(promise);
    }

    public async Task<PromiseDto> UpdateAsync(
        Guid userId, Guid promiseId, UpdatePromiseRequest request, CancellationToken cancellationToken = default)
    {
        var promise = await LoadOwnedAsync(userId, promiseId, cancellationToken);

        if (request.Status == "Cancelled")
        {
            if (promise.Redemptions.Count > 0)
            {
                throw new ConflictException("This promise already has money redeemed against it, it cannot be cancelled any more.");
            }

            promise.Status = PromiseStatus.Cancelled;
        }

        if (request.Amount is not null && promise.Redemptions.Count > 0)
        {
            throw new ConflictException("This promise already has money redeemed against it, its amount cannot change any more.");
        }

        promise.PersonName = request.PersonName ?? promise.PersonName;
        promise.Note = request.Note ?? promise.Note;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(promise);
    }

    public async Task DeleteAsync(Guid userId, Guid promiseId, CancellationToken cancellationToken = default)
    {
        var promise = await LoadOwnedAsync(userId, promiseId, cancellationToken);
        if (promise.Redemptions.Count > 0)
        {
            throw new ConflictException("This promise already has money redeemed against it, cancel it instead of deleting it.");
        }

        _dbContext.Promises.Remove(promise);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DomainPromise> LoadOwnedAsync(Guid userId, Guid promiseId, CancellationToken cancellationToken)
    {
        return await _dbContext.Promises.Include(p => p.Redemptions)
            .FirstOrDefaultAsync(p => p.Id == promiseId && p.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that promise.");
    }

    private static PromiseDto ToDto(DomainPromise promise)
    {
        var totalRedeemed = promise.Redemptions.Sum(r => r.Amount.Amount);
        var outstanding = Math.Max(0m, promise.Amount.Amount - totalRedeemed);

        return new PromiseDto(
            promise.Id, promise.PersonName, promise.Amount.Amount, promise.Note, promise.MadeOn,
            promise.Status.ToString(), totalRedeemed, outstanding, promise.CreatedAt,
            promise.Redemptions.OrderBy(r => r.RedeemedOn).Select(r => new PromiseRedemptionDto(
                r.Id, r.Amount.Amount, r.RedeemedOn,
                new AccountRefInput(r.SourceAccountKind.ToString(), r.SourceAccountCategoryId), r.Note, r.CreatedAt)).ToList());
    }
}
