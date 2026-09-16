namespace Natoshare.Domain.Ledger;

// What kind of event a ledger line represents. Phase 3 only ever writes Allocation,
// Income, Expense, DeficitCoverage, Transfer and Reversal, the rest exist here because
// the full ledger design already names them, later phases (month close, promises,
// loans) will start writing them too.
public enum LedgerEntryType
{
    Allocation,
    Income,
    Expense,
    Transfer,
    Reversal,
    Rollover,
    ExternalDeploy,
    DeficitCoverage,
    DeficitCarryForward,
    PromiseRedemption,
    LoanLink,
    Adjustment,
}

// Which real transaction caused a ledger line to be written, so a ledger row can
// always be traced back to the thing that created it.
public enum SourceTxnType
{
    Income,
    Expense,
    Reallocation,
    MonthClose,
    DeficitResolution,
    PromiseRedemption,
    LoanRepayment,
    AdminAdjustment,
}
