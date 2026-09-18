using Natoshare.Application.Insights;
using Natoshare.Application.Ledger;
using Natoshare.Application.PeopleAndMoney;

namespace Natoshare.Application.Dashboard;

public record RecentTransactionDto(Guid Id, string Description, decimal Amount, bool IsIncome, DateOnly OccurredOn);

public record OpenPromiseSummaryDto(Guid Id, string PersonName, decimal Outstanding);

// Everything the dashboard's hero screen needs in one call, so the page does not
// have to fan out to eight different endpoints itself. Every figure here is also
// available from its own endpoint (net position, balances, obligations, and so on),
// this is just the one-stop-shop version of them.
public record DashboardDto(
    NetPositionDto NetPosition,
    MonthSummaryDto Month,
    IReadOnlyList<CategoryBalanceDto> CategoryCards,
    IReadOnlyList<PacingInsightDto> PacingOverview,
    FlexiblePoolDto FlexiblePool,
    IReadOnlyList<ObligationItemDto> Obligations,
    IReadOnlyList<OpenPromiseSummaryDto> OpenPromises,
    decimal OutstandingLoansOut,
    decimal DebtsOwed,
    IReadOnlyList<RecentTransactionDto> RecentTransactions,
    int UnreadAlerts);
