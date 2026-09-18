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

    // Confirming a FixedAccount category's external transfer is its own action, not
    // part of closing the month (a user can confirm any time during the month, well
    // before they actually close it), so it needs its own source instead of sharing
    // MonthClose, keeping the two independently reversible.
    FixedAccountConfirmation,

    // The savings and deficit a month carries IN from the previous closed month are
    // posted when THIS month opens, not when the previous one closed. Keeping this
    // separate from MonthClose means reopening a month only undoes what closing IT
    // posted, never the carry-in it received when it first opened.
    MonthOpen,

    // Lending money out is its own action, separate from getting it back
    // (LoanRepayment already exists for that side), so each can be reversed on its
    // own without touching the other.
    LoanDisbursement,

    // Paying back money the user borrowed. Kept separate from LoanRepayment because
    // that one means the opposite direction, money coming back from a LoanOut.
    DebtRepayment,
}
