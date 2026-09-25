using Natoshare.Application.Ledger;

namespace Natoshare.Application.Months;

public record MonthSummaryListItemDto(int Year, int Month, string Status, decimal FixedIncomeSnapshot);

public record CategoryMonthDetailDto(
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
    bool ExternalTransferConfirmed,
    decimal? ExternalTransferAmount,
    DateTimeOffset? ExternalTransferConfirmedAt,
    decimal? SavedThisMonth,
    decimal? DeficitAtClose,
    string? DeficitResolvedVia,
    decimal? CarriedOutSavings,
    decimal? CarriedOutDeficit);

public record MonthDetailDto(
    int Year, int Month, string Status, decimal FixedIncomeSnapshot, DateTimeOffset? ClosedAt, IReadOnlyList<CategoryMonthDetailDto> Categories);

// --- Close preview --------------------------------------------------------------

public record FixedAccountToConfirmDto(Guid CategoryId, string Name, decimal Allocated);

public record ProjectedSavingDto(Guid CategoryId, string Name, decimal Saved);

public record ClosePreviewResult(
    string? MissingIncomeHint,
    IReadOnlyList<FixedAccountToConfirmDto> FixedAccountsToConfirm,
    IReadOnlyList<ProjectedSavingDto> ProjectedSavings,
    IReadOnlyList<DeficitListItemDto> Deficits,
    decimal TotalSavings);

// --- Confirm fixed account --------------------------------------------------------------

public record ConfirmFixedAccountRequest(Guid CategoryId, decimal Amount, DateOnly TransferredOn);

// --- Close --------------------------------------------------------------

public record CloseDeficitResolutionInput(Guid CategoryId, decimal Amount, string Method, Guid? SourceCategoryId, string? Note);

public record RebalanceInput(AccountRefInput FromAccount, AccountRefInput ToAccount, decimal Amount, string? Note);

// A placeholder shape matching the documented request field, Promise does not exist
// until Phase 6, so this is only ever accepted empty for now.
public record PromiseRedemptionInput(Guid PromiseId, decimal Amount, AccountRefInput SourceAccount);

public record CloseMonthRequest(
    List<ConfirmFixedAccountRequest>? FixedAccountConfirmations,
    List<CloseDeficitResolutionInput> DeficitResolutions,
    List<PromiseRedemptionInput>? PromiseRedemptions,
    List<RebalanceInput>? Rebalances);

public record CloseMonthResult(int Year, int Month, string Status, DateTimeOffset ClosedAt, IReadOnlyList<CategoryMonthDetailDto> Categories);

// --- Admin reopen --------------------------------------------------------------

public record ReopenMonthRequest(string Reason);
