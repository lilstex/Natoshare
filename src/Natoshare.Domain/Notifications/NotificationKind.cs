namespace Natoshare.Domain.Notifications;

// Every kind of notification Natoshare can ever raise. Phase 4 only ever produces the
// first four (pacing and deficit alerts), the rest are named here because the full
// vocabulary is already designed, but nothing creates them until the phase that owns
// their entity ships: month close (Phase 5), loans/debts/promises (Phase 6), recurring
// items (Phase 7).
public enum NotificationKind
{
    OverPaceCategory,
    OverspendCategory,
    CategoryInDeficit,
    SafeToSpendLow,
    MonthCloseReminder,
    FixedAccountUnconfirmed,
    CarriedDeficitApplied,
    MonthEndSummary,
    DebtDueSoon,
    DebtOverdue,
    LoanReturnDueSoon,
    LoanOverdue,
    PromiseReminder,
    RecurringItemDue,
}
