using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;

namespace Natoshare.Domain.PeopleAndMoney;

public enum PromiseStatus
{
    Open,
    PartiallyRedeemed,
    Redeemed,

    // A user calls off a promise before ever paying it, a manual, sticky choice like
    // WrittenOff on a LoanOut. A promise with any redemption already on it cannot be
    // cancelled, that money has genuinely left already.
    Cancelled,
}

// Money the user has told someone they will give them, but has not handed over yet.
// Counted as a liability in net position until it is redeemed or cancelled.
public class Promise
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public string PersonName { get; set; } = "";

    public Money Amount { get; init; }

    public string? Note { get; set; }

    public DateOnly MadeOn { get; init; }

    public PromiseStatus Status { get; set; } = PromiseStatus.Open;

    public DateTimeOffset CreatedAt { get; init; }

    public List<PromiseRedemption> Redemptions { get; set; } = [];
}

// One payment made toward fulfilling a Promise. Unlike a loan or debt repayment, a
// source account is always required, this is real money actually leaving the
// budget, not just an optional bookkeeping link.
public class PromiseRedemption
{
    public Guid Id { get; init; }

    public Guid PromiseId { get; init; }

    public Money Amount { get; init; }

    public DateOnly RedeemedOn { get; init; }

    public AccountKind SourceAccountKind { get; init; }

    public Guid? SourceAccountCategoryId { get; init; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
