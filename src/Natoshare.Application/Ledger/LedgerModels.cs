namespace Natoshare.Application.Ledger;

// One account as the API sees it, plain strings instead of enums so it reads
// naturally in JSON: { "kind": "Category", "categoryId": "..." }.
public record AccountRefInput(string Kind, Guid? CategoryId);

// --- Income --------------------------------------------------------------

public record LogIncomeRequest(string Type, decimal Amount, string Description, DateOnly? OccurredOn);

public record UpdateIncomeRequest(decimal? Amount, string? Description, DateOnly? OccurredOn);

public record IncomeSplitDto(Guid CategoryId, string CategoryName, decimal Amount);

public record IncomeDto(
    Guid Id,
    string Type,
    decimal Amount,
    string Description,
    DateOnly OccurredOn,
    string Status,
    IReadOnlyList<IncomeSplitDto> Splits);

// --- Expenses --------------------------------------------------------------

public record LogExpenseRequest(
    decimal Amount,
    string Description,
    string Source,
    Guid? CategoryId,
    string? SubCategory,
    DateOnly? OccurredOn,
    List<string>? Tags);

public record UpdateExpenseRequest(
    decimal? Amount,
    string? Description,
    Guid? CategoryId,
    string? SubCategory,
    DateOnly? OccurredOn,
    List<string>? Tags);

public record ExpenseDto(
    Guid Id,
    decimal Amount,
    string Description,
    string Source,
    Guid? CategoryId,
    string? SubCategory,
    DateOnly OccurredOn,
    string Status,
    IReadOnlyList<string> Tags);

public record PacingDto(string CategoryStatus, decimal Projected, decimal SafeToSpend);

public record ExpenseDeficitDto(Guid CategoryId, decimal Amount);

public record LogExpenseResult(ExpenseDto Expense, PacingDto Pacing, bool WentIntoDeficit, ExpenseDeficitDto? Deficit);

public record TagDto(Guid Id, string Name, int UsageCount);

// --- Deficits --------------------------------------------------------------

public record SuggestedSourceDto(string Kind, Guid? CategoryId, decimal AvailableToUse);

public record DeficitListItemDto(
    Guid CategoryId,
    string Name,
    decimal Amount,
    decimal CarriedInDeficit,
    IReadOnlyList<SuggestedSourceDto> SuggestedSources);

public record ResolveDeficitRequest(
    Guid CategoryId,
    int Year,
    int Month,
    decimal Amount,
    string Method,
    Guid? SourceCategoryId,
    string? Note);

public record DeficitResolutionDto(
    Guid Id,
    Guid CategoryId,
    decimal Amount,
    string Method,
    Guid? SourceCategoryId,
    DateOnly ResolvedOn,
    string? Note);

public record DeficitHistoryItemDto(int Year, int Month, Guid CategoryId, decimal Amount, string? Method);

// --- Reallocations --------------------------------------------------------------

public record CreateReallocationRequest(
    AccountRefInput FromAccount,
    AccountRefInput ToAccount,
    decimal Amount,
    string Reason,
    string? Note,
    DateOnly? OccurredOn);

public record ReallocationDto(
    Guid Id,
    AccountRefInput FromAccount,
    AccountRefInput ToAccount,
    decimal Amount,
    string Reason,
    DateOnly OccurredOn,
    string? Note);

// --- Balances --------------------------------------------------------------

public record MonthSummaryDto(int Year, int Month, string Status);

public record PaceDto(decimal Projected, string Status);

public record SafeToSpendDto(decimal Daily, decimal Weekly);

public record CategoryBalanceDto(
    Guid CategoryId,
    string Name,
    string Kind,
    decimal Allocated,
    decimal CarriedInSavings,
    decimal CarriedInDeficit,
    decimal Covered,
    decimal Funded,
    decimal Spent,
    decimal Available,
    decimal Deficit,
    decimal SavingsBalance,
    decimal DeployedBalance,
    PaceDto Pace,
    SafeToSpendDto SafeToSpend,
    bool IsLocked);

public record FlexiblePoolDto(decimal Balance);

public record TotalsDto(decimal Allocated, decimal Spent, decimal Available, decimal Deficit, decimal SavedToDate);

public record BalancesResult(
    MonthSummaryDto Month,
    IReadOnlyList<CategoryBalanceDto> Categories,
    FlexiblePoolDto FlexiblePool,
    TotalsDto Totals);

public record BalanceHistoryItemDto(int Year, int Month, decimal Allocated, decimal Spent, decimal Saved, decimal Deficit);

// --- Raw ledger feed --------------------------------------------------------------

public record LedgerEntryDto(
    Guid Id,
    string Account,
    Guid? AccountCategoryId,
    string EntryType,
    decimal Amount,
    string Direction,
    string SourceTxnType,
    Guid SourceTxnId,
    string? Note,
    DateTimeOffset CreatedAt);

// --- Month snapshot (used internally, not returned to a controller directly) -------

public class MonthCategorySnapshot
{
    public Guid CategoryId { get; init; }

    public string Name { get; init; } = "";

    public string Kind { get; init; } = "";

    public Domain.Common.Money Allocated { get; init; }

    public Domain.Common.Money CarriedInSavings { get; init; }

    public Domain.Common.Money CarriedInDeficit { get; init; }

    public Domain.Common.Money Spent { get; init; }

    public Domain.Common.Money Covered { get; init; }

    public Domain.Common.Money? ExternalTransferAmount { get; init; }

    // Rebuilds a real CategoryMonth from this snapshot, just so callers can reuse
    // the actual Funded/Available/Deficit math instead of copying it out by hand.
    // The Id fields are left blank on purpose, this is only ever used for its
    // numbers, never saved.
    public Domain.Budgeting.CategoryMonth ToCategoryMonth() => new()
    {
        CategoryId = CategoryId,
        AllocatedAmount = Allocated,
        CarriedInSavings = CarriedInSavings,
        CarriedInDeficit = CarriedInDeficit,
        SpentAmount = Spent,
        CoveredAmount = Covered,
        ExternalTransferAmount = ExternalTransferAmount,
    };
}

public class MonthSnapshot
{
    public int Year { get; init; }

    public int Month { get; init; }

    public string Status { get; init; } = "Open";

    public Guid? BudgetMonthId { get; init; }

    public Domain.Common.Money FixedIncomeSnapshot { get; init; }

    public List<MonthCategorySnapshot> Categories { get; init; } = [];
}
