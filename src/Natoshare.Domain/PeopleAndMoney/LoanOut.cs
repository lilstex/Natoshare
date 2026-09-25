using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;

namespace Natoshare.Domain.PeopleAndMoney;

public enum LoanOutStatus
{
    Outstanding,
    PartiallyRepaid,
    Repaid,

    // A user gives up on getting the money back. This is a manual, sticky choice,
    // once written off a loan never recomputes back to Outstanding on its own.
    WrittenOff,
}

// Money the user lent to someone else. Tracked here whether or not it ever touched
// a real account, LinkedSource is only set when the user actually wants that lending
// to show up as money leaving one of their own accounts.
public class LoanOut
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string BorrowerName { get; set; } = "";

    public Money Amount { get; init; }

    public DateOnly LentOn { get; init; }

    public DateOnly? ExpectedReturnOn { get; set; }

    public string? Note { get; set; }

    public LoanOutStatus Status { get; set; } = LoanOutStatus.Outstanding;

    // Where the lent money came from, if the user chose to link it. Null means this
    // loan is only tracked here, no ledger entry was ever posted for it.
    public AccountKind? LinkedSourceKind { get; set; }

    public Guid? LinkedSourceCategoryId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public List<LoanRepayment> Repayments { get; set; } = [];
}

// One payment the borrower made back on a LoanOut.
public class LoanRepayment
{
    public Guid Id { get; init; }

    public Guid LoanOutId { get; init; }

    public Money Amount { get; init; }

    public DateOnly ReceivedOn { get; init; }

    public string? Note { get; init; }

    // Usually FlexiblePool, but the user can choose to land it back in a category
    // instead. Null means the money was received but never put back into any of
    // the user's tracked accounts.
    public AccountKind? LinkedDestinationKind { get; set; }

    public Guid? LinkedDestinationCategoryId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
