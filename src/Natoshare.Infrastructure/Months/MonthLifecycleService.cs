using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.Months;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Common;
using Natoshare.Domain.Identity;
using Natoshare.Domain.Ledger;
using Natoshare.Domain.Notifications;
using Natoshare.Domain.Transactions;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Months;

// The close-month ritual: confirming fixed accounts, resolving whatever is still in
// deficit, rolling surplus into savings, and freezing the month for good. Everything
// here either reuses the same building blocks Phase 3/4 already built (the ledger,
// reallocations, deficit resolutions) or extends them the same way, nothing about how
// money actually moves is reinvented just because it is month-end.
public class MonthLifecycleService : IMonthLifecycleService
{
    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ILedgerService _ledgerService;
    private readonly IBudgetMonthService _budgetMonthService;
    private readonly IReallocationService _reallocationService;
    private readonly IDeficitService _deficitService;
    private readonly IAuditLogger _auditLogger;

    public MonthLifecycleService(
        NatoshareDbContext dbContext,
        IClock clock,
        ILedgerService ledgerService,
        IBudgetMonthService budgetMonthService,
        IReallocationService reallocationService,
        IDeficitService deficitService,
        IAuditLogger auditLogger)
    {
        _dbContext = dbContext;
        _clock = clock;
        _ledgerService = ledgerService;
        _budgetMonthService = budgetMonthService;
        _reallocationService = reallocationService;
        _deficitService = deficitService;
        _auditLogger = auditLogger;
    }

    public async Task<IReadOnlyList<MonthSummaryListItemDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var months = await _dbContext.BudgetMonths
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.Year).ThenByDescending(m => m.Month)
            .ToListAsync(cancellationToken);

        return months.Select(m => new MonthSummaryListItemDto(m.Year, m.Month, m.Status.ToString(), m.FixedIncomeSnapshot.Amount)).ToList();
    }

    public async Task<MonthDetailDto> GetDetailAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var budgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == year && m.Month == month, cancellationToken);

        if (budgetMonth is null)
        {
            // Not materialised yet, the same live (nothing saved) preview /balances
            // uses for the current month works just as well here.
            var snapshot = await _budgetMonthService.GetSnapshotAsync(userId, year, month, cancellationToken);
            return new MonthDetailDto(
                year, month, snapshot.Status, snapshot.FixedIncomeSnapshot.Amount, null,
                snapshot.Categories.Select(ToPreviewDetailDto).ToList());
        }

        var categories = await CategoryLookupAsync(userId, cancellationToken);
        return new MonthDetailDto(
            year, month, budgetMonth.Status.ToString(), budgetMonth.FixedIncomeSnapshot.Amount, budgetMonth.ClosedAt,
            budgetMonth.CategoryMonths.Select(cm => ToDetailDto(cm, categories)).ToList());
    }

    public async Task<MonthDetailDto> OpenEarlyAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        await _budgetMonthService.EnsureOpenAsync(userId, year, month, cancellationToken);
        return await GetDetailAsync(userId, year, month, cancellationToken);
    }

    public async Task<ClosePreviewResult> GetClosePreviewAsync(Guid userId, int year, int month, CancellationToken cancellationToken = default)
    {
        var budgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == year && m.Month == month, cancellationToken)
            ?? throw new NotFoundException("We could not find that month.");

        var categories = await CategoryLookupAsync(userId, cancellationToken);

        var loggedIncome = budgetMonth.CategoryMonths.Sum(cm => cm.AllocatedAmount.Amount);
        var missingIncomeHint = loggedIncome < budgetMonth.FixedIncomeSnapshot.Amount
            ? "You have logged less income than usual this month, double check before closing."
            : null;

        var fixedAccountsToConfirm = budgetMonth.CategoryMonths
            .Where(cm => IsUnconfirmedFixedAccount(cm, categories))
            .Select(cm => new FixedAccountToConfirmDto(cm.CategoryId, NameFor(cm.CategoryId, categories), cm.AllocatedAmount.Amount))
            .ToList();

        var projectedSavings = budgetMonth.CategoryMonths
            .Where(cm => cm.Deficit() == Money.Zero)
            .Select(cm => new ProjectedSavingDto(cm.CategoryId, NameFor(cm.CategoryId, categories), ProjectedSavedFor(cm, categories).Amount))
            .Where(p => p.Saved > 0m)
            .ToList();

        var deficits = await _deficitService.ListAsync(userId, year, month, "open", cancellationToken);

        return new ClosePreviewResult(missingIncomeHint, fixedAccountsToConfirm, projectedSavings, deficits, projectedSavings.Sum(p => p.Saved));
    }

    public async Task<CategoryMonthDetailDto> ConfirmFixedAccountAsync(
        Guid userId, int year, int month, ConfirmFixedAccountRequest request, CancellationToken cancellationToken = default)
    {
        var budgetMonth = await _dbContext.BudgetMonths
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == year && m.Month == month, cancellationToken)
            ?? throw new NotFoundException("We could not find that month.");

        if (budgetMonth.Status == BudgetMonthStatus.Closed)
        {
            throw new ConflictException("This month is already closed.");
        }

        var category = await _dbContext.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that category.");

        if (category.Kind != CategoryKind.FixedAccount)
        {
            throw new ConflictException("Only a fixed account category can be confirmed this way.");
        }

        var categoryMonth = await _dbContext.CategoryMonths
            .FirstOrDefaultAsync(cm => cm.BudgetMonthId == budgetMonth.Id && cm.CategoryId == request.CategoryId, cancellationToken)
            ?? throw new NotFoundException("We could not find that category for this month.");

        await ConfirmFixedAccountInternalAsync(userId, budgetMonth, categoryMonth, request, cancellationToken);

        var categories = await CategoryLookupAsync(userId, cancellationToken);
        return ToDetailDto(categoryMonth, categories);
    }

    public async Task<CloseMonthResult> CloseAsync(
        Guid userId, int year, int month, CloseMonthRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var budgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.Year == year && m.Month == month, cancellationToken)
            ?? throw new NotFoundException("We could not find that month.");

        if (budgetMonth.Status == BudgetMonthStatus.Closed)
        {
            throw new ConflictException("This month is already closed.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var categories = await CategoryLookupAsync(userId, cancellationToken);

        // Fixed account confirmations and rebalances bundled into the close call
        // itself are applied first, they change the numbers everything below is
        // based on.
        if (request.FixedAccountConfirmations is not null)
        {
            foreach (var confirmation in request.FixedAccountConfirmations)
            {
                var categoryMonth = budgetMonth.CategoryMonths.FirstOrDefault(cm => cm.CategoryId == confirmation.CategoryId)
                    ?? throw new NotFoundException("We could not find that category for this month.");
                await ConfirmFixedAccountInternalAsync(userId, budgetMonth, categoryMonth, confirmation, cancellationToken);
            }
        }

        if (request.Rebalances is not null)
        {
            foreach (var rebalance in request.Rebalances)
            {
                await _reallocationService.CreateAsync(
                    userId,
                    new CreateReallocationRequest(
                        rebalance.FromAccount, rebalance.ToAccount, rebalance.Amount, "MonthCloseRebalance", rebalance.Note, null),
                    cancellationToken);
            }
        }

        // Every category still in deficit at this point needs a resolution supplied
        // right here, closing cannot go ahead otherwise.
        var stillInDeficit = budgetMonth.CategoryMonths.Where(cm => cm.Deficit() > Money.Zero).ToList();
        var providedByCategory = request.DeficitResolutions.ToDictionary(d => d.CategoryId);

        var unresolved = stillInDeficit.Where(cm => !providedByCategory.ContainsKey(cm.CategoryId)).ToList();
        if (unresolved.Count > 0)
        {
            var names = string.Join(", ", unresolved.Select(cm => NameFor(cm.CategoryId, categories)));
            throw new ConflictException($"{names} still {(unresolved.Count == 1 ? "has" : "have")} an unresolved deficit.");
        }

        // Savings roll over BEFORE deficits get resolved, on purpose: covering a
        // deficit from "another category's savings" only has money to draw on once
        // that other category's own leftover has actually been swept into its
        // savings account (matching the worked example in 01-domain-model.md §5,
        // Feeding's savings roll over first, then Transportation draws on them).
        foreach (var categoryMonth in budgetMonth.CategoryMonths)
        {
            if (categoryMonth.Deficit() > Money.Zero)
            {
                continue; // nothing to save, this one is handled below instead
            }

            var saved = ProjectedSavedFor(categoryMonth, categories);
            categoryMonth.SavedThisMonth = saved;
            categoryMonth.CarriedOutSavings = saved;
            categoryMonth.DeficitAtClose = Money.Zero;

            if (saved > Money.Zero)
            {
                await _ledgerService.PostAsync(
                    userId, budgetMonth.Id, AccountRef.CategorySavings(categoryMonth.CategoryId), LedgerEntryType.Rollover,
                    saved, LedgerDirection.Credit, SourceTxnType.MonthClose, budgetMonth.Id, "Rolled to savings at close", cancellationToken);
                await _ledgerService.PostAsync(
                    userId, budgetMonth.Id, AccountRef.Category(categoryMonth.CategoryId), LedgerEntryType.Rollover,
                    saved, LedgerDirection.Debit, SourceTxnType.MonthClose, budgetMonth.Id, "Rolled to savings at close", cancellationToken);
            }
        }

        foreach (var resolutionInput in request.DeficitResolutions)
        {
            var categoryMonth = budgetMonth.CategoryMonths.FirstOrDefault(cm => cm.CategoryId == resolutionInput.CategoryId);
            if (categoryMonth is null || categoryMonth.Deficit() == Money.Zero)
            {
                continue; // already resolved earlier in the month, nothing left to do
            }

            await ApplyDeficitResolutionAsync(user, budgetMonth, categoryMonth, resolutionInput, cancellationToken);

            // Covering this deficit from another category's savings reduces what
            // that category actually has left to carry out, its CarriedOutSavings
            // above was set before this money moved, so it needs correcting now.
            if (resolutionInput.Method == "OtherCategorySavings" && resolutionInput.SourceCategoryId is { } sourceCategoryId)
            {
                var sourceCategoryMonth = budgetMonth.CategoryMonths.FirstOrDefault(cm => cm.CategoryId == sourceCategoryId);
                if (sourceCategoryMonth?.CarriedOutSavings is { } currentSaved)
                {
                    sourceCategoryMonth.CarriedOutSavings = new Money(Math.Max(0m, currentSaved.Amount - categoryMonth.DeficitAtClose!.Value.Amount));
                }
            }
        }

        budgetMonth.Status = BudgetMonthStatus.Closed;
        budgetMonth.ClosedAt = _clock.UtcNow;
        budgetMonth.ClosedByUserId = userId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // The one invariant that matters most: every category account for this
        // month must now net to exactly zero. A failure here means a bug upstream,
        // not something the user did wrong, so this throws (rolling back the whole
        // close via the transaction) rather than closing with numbers that do not
        // add up.
        foreach (var categoryMonth in budgetMonth.CategoryMonths)
        {
            var balance = await _ledgerService.GetAccountBalanceAsync(userId, AccountRef.Category(categoryMonth.CategoryId), cancellationToken);
            if (balance != Money.Zero)
            {
                throw new InvalidOperationException(
                    $"Category {categoryMonth.CategoryId} did not net to zero after close (was {balance.Amount}).");
            }
        }

        await transaction.CommitAsync(cancellationToken);

        await NotifyMonthEndSummaryAsync(user, budgetMonth, cancellationToken);
        await _auditLogger.LogAsync(userId, "User", "MonthClosed", "BudgetMonth", budgetMonth.Id.ToString(), null, null, cancellationToken);

        return new CloseMonthResult(
            year, month, budgetMonth.Status.ToString(), budgetMonth.ClosedAt.Value,
            budgetMonth.CategoryMonths.Select(cm => ToDetailDto(cm, categories)).ToList());
    }

    public async Task ReopenAsync(
        Guid adminUserId, Guid targetUserId, int year, int month, string reason, CancellationToken cancellationToken = default)
    {
        var budgetMonth = await _dbContext.BudgetMonths
            .Include(m => m.CategoryMonths)
            .FirstOrDefaultAsync(m => m.UserId == targetUserId && m.Year == year && m.Month == month, cancellationToken)
            ?? throw new NotFoundException("We could not find that month.");

        if (budgetMonth.Status != BudgetMonthStatus.Closed)
        {
            throw new ConflictException("This month is not closed.");
        }

        // Undo what closing itself posted (rollovers and carried-forward deficits).
        // A mid-month or close-time deficit cover through a reallocation, and any
        // fixed account confirmation, already moved real money and stays standing,
        // reopening is about letting someone keep working on the month, not
        // rewinding every choice made in it.
        await _ledgerService.ReverseAsync(SourceTxnType.MonthClose, budgetMonth.Id, $"Reopened: {reason}", cancellationToken);

        foreach (var categoryMonth in budgetMonth.CategoryMonths)
        {
            categoryMonth.SavedThisMonth = null;
            categoryMonth.DeficitAtClose = null;
            categoryMonth.DeficitResolvedVia = null;
            categoryMonth.CarriedOutSavings = null;
            categoryMonth.CarriedOutDeficit = null;
        }

        budgetMonth.Status = BudgetMonthStatus.Open;
        budgetMonth.ClosedAt = null;
        budgetMonth.ClosedByUserId = null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _auditLogger.LogAsync(adminUserId, "Admin", "MonthReopened", "BudgetMonth", budgetMonth.Id.ToString(), null, null, cancellationToken);
    }

    public async Task EvaluateCloseRemindersAsync(CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users.ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var preference = await _dbContext.AlertPreferences
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Kind == NotificationKind.MonthCloseReminder, cancellationToken);

            if (preference is { Enabled: false })
            {
                continue;
            }

            var leadDays = preference?.LeadDays ?? 3;
            var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
            var currentMonthStart = new DateOnly(today.Year, today.Month, 1);

            var openMonths = await _dbContext.BudgetMonths
                .Where(m => m.UserId == user.Id && m.Status == BudgetMonthStatus.Open)
                .ToListAsync(cancellationToken);

            foreach (var month in openMonths)
            {
                var monthStart = new DateOnly(month.Year, month.Month, 1);
                if (monthStart >= currentMonthStart)
                {
                    continue; // this is the current month, not overdue yet
                }

                var monthEnd = monthStart.AddMonths(1).AddDays(-1);
                var daysSinceEnd = today.DayNumber - monthEnd.DayNumber;
                if (daysSinceEnd < leadDays)
                {
                    continue;
                }

                if (await AlreadyRemindedTodayAsync(user.Id, month.Id, today, user.TimeZoneId, cancellationToken))
                {
                    continue;
                }

                var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month.Month);
                _dbContext.Notifications.Add(new Notification
                {
                    Id = Guid.CreateVersion7(),
                    UserId = user.Id,
                    Kind = NotificationKind.MonthCloseReminder,
                    Title = $"{monthName} {month.Year} is still open",
                    Body = $"It has been {daysSinceEnd} day(s) since {monthName} ended, close it out to roll over savings and see how you did.",
                    Severity = NotificationSeverity.Warning,
                    RelatedEntityType = "BudgetMonth",
                    RelatedEntityId = month.Id.ToString(),
                    CreatedAt = _clock.UtcNow,
                });
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> AlreadyRemindedTodayAsync(
        Guid userId, Guid budgetMonthId, DateOnly today, string timeZoneId, CancellationToken cancellationToken)
    {
        var relatedEntityId = budgetMonthId.ToString();
        var mostRecent = await _dbContext.Notifications
            .Where(n => n.UserId == userId && n.Kind == NotificationKind.MonthCloseReminder && n.RelatedEntityId == relatedEntityId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => (DateTimeOffset?)n.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return mostRecent is not null && UserTime.TodayFor(timeZoneId, mostRecent.Value) == today;
    }

    private async Task ConfirmFixedAccountInternalAsync(
        Guid userId, BudgetMonth budgetMonth, CategoryMonth categoryMonth, ConfirmFixedAccountRequest request, CancellationToken cancellationToken)
    {
        // Re-confirming (a different amount or date) undoes the old transfer first,
        // so it is never double counted.
        if (categoryMonth.ExternalTransferConfirmed)
        {
            await _ledgerService.ReverseAsync(SourceTxnType.FixedAccountConfirmation, categoryMonth.Id, "Re-confirmed", cancellationToken);
        }

        var amount = new Money(request.Amount);
        categoryMonth.ExternalTransferConfirmed = true;
        categoryMonth.ExternalTransferAmount = amount;
        categoryMonth.ExternalTransferConfirmedAt = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var note = $"Transferred on {request.TransferredOn:yyyy-MM-dd}";
        await _ledgerService.PostAsync(
            userId, budgetMonth.Id, AccountRef.External(categoryMonth.CategoryId), LedgerEntryType.ExternalDeploy,
            amount, LedgerDirection.Credit, SourceTxnType.FixedAccountConfirmation, categoryMonth.Id, note, cancellationToken);
        await _ledgerService.PostAsync(
            userId, budgetMonth.Id, AccountRef.Category(categoryMonth.CategoryId), LedgerEntryType.ExternalDeploy,
            amount, LedgerDirection.Debit, SourceTxnType.FixedAccountConfirmation, categoryMonth.Id, note, cancellationToken);
    }

    private async Task ApplyDeficitResolutionAsync(
        User user, BudgetMonth budgetMonth, CategoryMonth categoryMonth, CloseDeficitResolutionInput input, CancellationToken cancellationToken)
    {
        // The amount that actually gets covered is always the real outstanding
        // deficit at the moment this runs, not whatever the client happened to send,
        // so the numbers are guaranteed to add up to exactly zero afterwards.
        var deficitAtClose = categoryMonth.Deficit();
        var method = Enum.Parse<DeficitResolutionMethod>(input.Method);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);

        Guid? reallocationId = null;

        if (method == DeficitResolutionMethod.NextMonthAllocation)
        {
            await _ledgerService.PostAsync(
                user.Id, budgetMonth.Id, AccountRef.Category(categoryMonth.CategoryId), LedgerEntryType.DeficitCarryForward,
                deficitAtClose, LedgerDirection.Credit, SourceTxnType.MonthClose, budgetMonth.Id, "Carried to next month", cancellationToken);

            categoryMonth.CarriedOutDeficit = deficitAtClose;
        }
        else
        {
            var (fromKind, fromCategoryId) = method switch
            {
                DeficitResolutionMethod.OwnSavings => ("CategorySavings", (Guid?)categoryMonth.CategoryId),
                DeficitResolutionMethod.OtherCategorySavings => ("CategorySavings", input.SourceCategoryId),
                DeficitResolutionMethod.FlexiblePool => ("FlexiblePool", (Guid?)null),
                _ => throw new ConflictException("This deficit resolution method is not supported."),
            };

            var reallocation = await _reallocationService.CreateAsync(
                user.Id,
                new CreateReallocationRequest(
                    new AccountRefInput(fromKind, fromCategoryId), new AccountRefInput("Category", categoryMonth.CategoryId),
                    deficitAtClose.Amount, "DeficitCover", input.Note, null),
                cancellationToken);

            reallocationId = reallocation.Id;
            categoryMonth.CarriedOutDeficit = Money.Zero;
        }

        categoryMonth.DeficitAtClose = deficitAtClose;
        categoryMonth.DeficitResolvedVia = method;

        _dbContext.DeficitResolutions.Add(new DeficitResolution
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            BudgetMonthId = budgetMonth.Id,
            CategoryId = categoryMonth.CategoryId,
            Amount = deficitAtClose,
            Method = method,
            SourceCategoryId = input.SourceCategoryId,
            ResolvedOn = today,
            ResolvedByUserId = user.Id,
            Note = input.Note,
            ReallocationId = reallocationId,
            CreatedAt = _clock.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NotifyMonthEndSummaryAsync(User user, BudgetMonth budgetMonth, CancellationToken cancellationToken)
    {
        var preference = await _dbContext.AlertPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.Id && p.Kind == NotificationKind.MonthEndSummary, cancellationToken);

        if (preference is { Enabled: false })
        {
            return;
        }

        var totalSpent = budgetMonth.CategoryMonths.Sum(cm => cm.SpentAmount.Amount);
        var totalSaved = budgetMonth.CategoryMonths.Sum(cm => cm.SavedThisMonth?.Amount ?? 0m);
        var monthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(budgetMonth.Month);

        _dbContext.Notifications.Add(new Notification
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            Kind = NotificationKind.MonthEndSummary,
            Title = $"{monthName} {budgetMonth.Year} is closed",
            Body = $"You spent {user.CurrencySymbol}{totalSpent:N2} and saved {user.CurrencySymbol}{totalSaved:N2} this month.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = "BudgetMonth",
            RelatedEntityId = budgetMonth.Id.ToString(),
            CreatedAt = _clock.UtcNow,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // A confirmed fixed account already had its money moved out externally, there is
    // nothing left in the category to save. Anything else (Standard, or a
    // FixedAccount nobody confirmed yet) just rolls whatever is left into its own
    // savings, money never just vanishes because nobody clicked a confirm button.
    private static Money ProjectedSavedFor(CategoryMonth categoryMonth, Dictionary<Guid, Category> categories)
    {
        if (categories.TryGetValue(categoryMonth.CategoryId, out var category)
            && category.Kind == CategoryKind.FixedAccount && categoryMonth.ExternalTransferConfirmed)
        {
            return Money.Zero;
        }

        return categoryMonth.Available();
    }

    private static bool IsUnconfirmedFixedAccount(CategoryMonth categoryMonth, Dictionary<Guid, Category> categories) =>
        categories.TryGetValue(categoryMonth.CategoryId, out var category)
        && category.Kind == CategoryKind.FixedAccount
        && !categoryMonth.ExternalTransferConfirmed;

    private async Task<Dictionary<Guid, Category>> CategoryLookupAsync(Guid userId, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.Categories.Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        return categories.ToDictionary(c => c.Id);
    }

    private static string NameFor(Guid categoryId, Dictionary<Guid, Category> categories) =>
        categories.TryGetValue(categoryId, out var category) ? category.Name : "(deleted category)";

    private static CategoryMonthDetailDto ToDetailDto(CategoryMonth cm, Dictionary<Guid, Category> categories)
    {
        categories.TryGetValue(cm.CategoryId, out var category);
        return new CategoryMonthDetailDto(
            cm.CategoryId,
            category?.Name ?? "(deleted category)",
            category?.Kind.ToString() ?? "Standard",
            cm.AllocatedAmount.Amount,
            cm.CarriedInSavings.Amount,
            cm.CarriedInDeficit.Amount,
            cm.CoveredAmount.Amount,
            cm.Funded().Amount,
            cm.SpentAmount.Amount,
            cm.Available().Amount,
            cm.Deficit().Amount,
            cm.ExternalTransferConfirmed,
            cm.ExternalTransferAmount?.Amount,
            cm.ExternalTransferConfirmedAt,
            cm.SavedThisMonth?.Amount,
            cm.DeficitAtClose?.Amount,
            cm.DeficitResolvedVia?.ToString(),
            cm.CarriedOutSavings?.Amount,
            cm.CarriedOutDeficit?.Amount);
    }

    private static CategoryMonthDetailDto ToPreviewDetailDto(MonthCategorySnapshot snapshot)
    {
        var categoryMonth = snapshot.ToCategoryMonth();
        return new CategoryMonthDetailDto(
            snapshot.CategoryId,
            snapshot.Name,
            snapshot.Kind,
            snapshot.Allocated.Amount,
            snapshot.CarriedInSavings.Amount,
            snapshot.CarriedInDeficit.Amount,
            snapshot.Covered.Amount,
            categoryMonth.Funded().Amount,
            snapshot.Spent.Amount,
            categoryMonth.Available().Amount,
            categoryMonth.Deficit().Amount,
            false,
            snapshot.ExternalTransferAmount?.Amount,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
