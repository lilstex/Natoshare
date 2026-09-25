using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;

namespace Natoshare.Domain.PeopleAndMoney;

public enum DebtInStatus
{
    Outstanding,
    PartiallyRepaid,
    Repaid,
}

// Money the user borrowed from someone else. Borrowing itself never touches the
// ledger, only paying it back does (see DebtRepayment.LinkedSource), since the
// borrowed cash usually never passes through one of the user's tracked accounts.
public class DebtIn
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string LenderName { get; set; } = "";

    public Money Amount { get; init; }

    public DateOnly BorrowedOn { get; init; }

    public DateOnly? DueOn { get; set; }

    public string? Note { get; set; }

    public DebtInStatus Status { get; set; } = DebtInStatus.Outstanding;

    public DateTimeOffset CreatedAt { get; init; }

    public List<DebtRepayment> Repayments { get; set; } = [];
}

// One payment the user made back on a DebtIn.
public class DebtRepayment
{
    public Guid Id { get; init; }

    public Guid DebtInId { get; init; }

    public Money Amount { get; init; }

    public DateOnly PaidOn { get; init; }

    public string? Note { get; set; }

    // Which of the user's own accounts the repayment came out of, if any.
    public AccountKind? LinkedSourceKind { get; set; }

    public Guid? LinkedSourceCategoryId { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
