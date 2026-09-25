using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Common;
using Natoshare.Application.Ledger;
using Natoshare.Application.Planning;
using Natoshare.Domain.Common;
using Natoshare.Domain.Notifications;
using Natoshare.Domain.Planning;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Planning;

// Recurring items: creating, editing and skipping them (all gated behind an active
// entitlement, this whole feature is marked 💳), and the hourly job that actually
// acts on the ones that are due.
public class RecurringItemService : IRecurringItemService, IRecurringItemMaterializer
{
    private const string FeatureName = "Recurring items";

    private readonly NatoshareDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IEntitlementService _entitlementService;
    private readonly IIncomeService _incomeService;
    private readonly IExpenseService _expenseService;

    public RecurringItemService(
        NatoshareDbContext dbContext,
        IClock clock,
        IEntitlementService entitlementService,
        IIncomeService incomeService,
        IExpenseService expenseService)
    {
        _dbContext = dbContext;
        _clock = clock;
        _entitlementService = entitlementService;
        _incomeService = incomeService;
        _expenseService = expenseService;
    }

    public async Task<IReadOnlyList<RecurringItemDto>> ListAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _entitlementService.EnsureEntitledAsync(userId, FeatureName, cancellationToken);

        var items = await _dbContext.RecurringItems
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.NextRunOn)
            .ToListAsync(cancellationToken);

        return items.Select(ToDto).ToList();
    }

    public async Task<RecurringItemDto> CreateAsync(Guid userId, CreateRecurringItemRequest request, CancellationToken cancellationToken = default)
    {
        await _entitlementService.EnsureEntitledAsync(userId, FeatureName, cancellationToken);

        var kind = Enum.Parse<RecurringItemKind>(request.Kind);
        if (kind == RecurringItemKind.Expense && request.CategoryId is not null)
        {
            var ownsCategory = await _dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId && c.UserId == userId, cancellationToken);
            if (!ownsCategory)
            {
                throw new NotFoundException("We could not find that category.");
            }
        }

        var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
        var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
        var cadence = Enum.Parse<RecurringCadence>(request.Cadence);

        var item = new RecurringItem
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Kind = kind,
            Amount = new Money(request.Amount),
            Description = request.Description,
            CategoryId = kind == RecurringItemKind.Expense ? request.CategoryId : null,
            IncomeType = kind == RecurringItemKind.Income ? Enum.Parse<Domain.Transactions.IncomeType>(request.IncomeType!) : null,
            Cadence = cadence,
            AnchorDay = request.AnchorDay,
            Mode = Enum.Parse<RecurringItemMode>(request.Mode),
            NextRunOn = RecurringScheduleCalculator.FirstRunOn(cadence, request.AnchorDay, today),
            IsActive = true,
            CreatedAt = _clock.UtcNow,
        };

        _dbContext.RecurringItems.Add(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<RecurringItemDto> UpdateAsync(
        Guid userId, Guid recurringItemId, UpdateRecurringItemRequest request, CancellationToken cancellationToken = default)
    {
        await _entitlementService.EnsureEntitledAsync(userId, FeatureName, cancellationToken);
        var item = await LoadOwnedAsync(userId, recurringItemId, cancellationToken);

        item.Amount = request.Amount is not null ? new Money(request.Amount.Value) : item.Amount;
        item.Description = request.Description ?? item.Description;
        item.IsActive = request.IsActive ?? item.IsActive;

        if (item.Kind == RecurringItemKind.Expense && request.CategoryId is not null)
        {
            var ownsCategory = await _dbContext.Categories.AnyAsync(c => c.Id == request.CategoryId && c.UserId == userId, cancellationToken);
            if (!ownsCategory)
            {
                throw new NotFoundException("We could not find that category.");
            }

            item.CategoryId = request.CategoryId;
        }

        if (item.Kind == RecurringItemKind.Income && request.IncomeType is not null)
        {
            item.IncomeType = Enum.Parse<Domain.Transactions.IncomeType>(request.IncomeType);
        }

        if (request.Mode is not null)
        {
            item.Mode = Enum.Parse<RecurringItemMode>(request.Mode);
        }

        // Changing the schedule itself means the old NextRunOn no longer means
        // anything, work out a fresh one from today instead of leaving it stale.
        if (request.Cadence is not null || request.AnchorDay is not null)
        {
            item.Cadence = request.Cadence is not null ? Enum.Parse<RecurringCadence>(request.Cadence) : item.Cadence;
            item.AnchorDay = request.AnchorDay ?? item.AnchorDay;

            var user = await _dbContext.Users.FirstAsync(u => u.Id == userId, cancellationToken);
            var today = UserTime.TodayFor(user.TimeZoneId, _clock.UtcNow);
            item.NextRunOn = RecurringScheduleCalculator.FirstRunOn(item.Cadence, item.AnchorDay, today);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task DeleteAsync(Guid userId, Guid recurringItemId, CancellationToken cancellationToken = default)
    {
        await _entitlementService.EnsureEntitledAsync(userId, FeatureName, cancellationToken);
        var item = await LoadOwnedAsync(userId, recurringItemId, cancellationToken);

        _dbContext.RecurringItems.Remove(item);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RecurringItemDto> SkipNextAsync(Guid userId, Guid recurringItemId, CancellationToken cancellationToken = default)
    {
        await _entitlementService.EnsureEntitledAsync(userId, FeatureName, cancellationToken);
        var item = await LoadOwnedAsync(userId, recurringItemId, cancellationToken);

        item.NextRunOn = RecurringScheduleCalculator.AdvanceOnce(item.Cadence, item.AnchorDay, item.NextRunOn);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(item);
    }

    public async Task<CommittedTotalDto> GetCommittedTotalAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await _entitlementService.EnsureEntitledAsync(userId, FeatureName, cancellationToken);

        var expenses = await _dbContext.RecurringItems
            .Where(r => r.UserId == userId && r.IsActive && r.Kind == RecurringItemKind.Expense)
            .ToListAsync(cancellationToken);

        var items = expenses
            .Select(r => new CommittedTotalItemDto(r.Id, r.Description, RecurringScheduleCalculator.NormalizeToMonthly(r.Cadence, r.Amount.Amount)))
            .ToList();

        return new CommittedTotalDto(items.Sum(i => i.MonthlyAmount), items);
    }

    // Run hourly (see docs/03-architecture.md): catches up every active item whose
    // schedule has arrived, for as many cycles as it takes to reach "today" (in
    // case a job run was ever missed), skipping anything owned by a lapsed trial.
    public async Task MaterializeDueItemsAsync(CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users.Select(u => new { u.Id, u.TimeZoneId, u.TrialEndsAt }).ToListAsync(cancellationToken);
        var now = _clock.UtcNow;

        foreach (var userRow in users)
        {
            if (now >= userRow.TrialEndsAt)
            {
                continue;
            }

            var today = UserTime.TodayFor(userRow.TimeZoneId, now);
            var preferences = await _dbContext.AlertPreferences
                .Where(p => p.UserId == userRow.Id && p.Kind == NotificationKind.RecurringItemDue)
                .ToListAsync(cancellationToken);
            var remindersEnabled = preferences.FirstOrDefault() is not { Enabled: false };

            var dueItems = await _dbContext.RecurringItems
                .Where(r => r.UserId == userRow.Id && r.IsActive && r.NextRunOn <= today)
                .ToListAsync(cancellationToken);

            foreach (var item in dueItems)
            {
                // A safety cap, not a real expectation, an hourly job should never
                // actually fall this many cycles behind.
                for (var i = 0; i < 24 && item.NextRunOn <= today; i++)
                {
                    await ProcessOneOccurrenceAsync(userRow.Id, item, remindersEnabled, cancellationToken);
                }
            }
        }
    }

    private async Task ProcessOneOccurrenceAsync(Guid userId, RecurringItem item, bool remindersEnabled, CancellationToken cancellationToken)
    {
        if (item.Mode == RecurringItemMode.AutoPost)
        {
            if (item.Kind == RecurringItemKind.Expense)
            {
                await _expenseService.CreateAsync(
                    userId,
                    new LogExpenseRequest(
                        item.Amount.Amount, item.Description, item.CategoryId is null ? "FlexiblePool" : "Category",
                        item.CategoryId, null, item.NextRunOn, null),
                    cancellationToken);
            }
            else
            {
                await _incomeService.CreateAsync(
                    userId,
                    new LogIncomeRequest(item.IncomeType!.Value.ToString(), item.Amount.Amount, item.Description, item.NextRunOn),
                    cancellationToken);
            }

            item.LastPostedOn = item.NextRunOn;
        }
        else if (remindersEnabled)
        {
            _dbContext.Notifications.Add(new Notification
            {
                Id = Guid.CreateVersion7(),
                UserId = userId,
                Kind = NotificationKind.RecurringItemDue,
                Title = $"{item.Description} is due",
                Body = $"Your recurring {item.Kind.ToString().ToLowerInvariant()} \"{item.Description}\" is due, log it whenever you are ready.",
                Severity = NotificationSeverity.Info,
                RelatedEntityType = "RecurringItem",
                RelatedEntityId = item.Id.ToString(),
                CreatedAt = _clock.UtcNow,
            });
        }

        item.NextRunOn = RecurringScheduleCalculator.AdvanceOnce(item.Cadence, item.AnchorDay, item.NextRunOn);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<RecurringItem> LoadOwnedAsync(Guid userId, Guid recurringItemId, CancellationToken cancellationToken)
    {
        return await _dbContext.RecurringItems.FirstOrDefaultAsync(r => r.Id == recurringItemId && r.UserId == userId, cancellationToken)
            ?? throw new NotFoundException("We could not find that recurring item.");
    }

    private static RecurringItemDto ToDto(RecurringItem item) => new(
        item.Id, item.Kind.ToString(), item.Amount.Amount, item.Description, item.CategoryId, item.IncomeType?.ToString(),
        item.Cadence.ToString(), item.AnchorDay, item.Mode.ToString(), item.NextRunOn, item.LastPostedOn, item.IsActive, item.CreatedAt);
}
