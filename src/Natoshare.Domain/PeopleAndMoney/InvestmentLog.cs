using Natoshare.Domain.Common;

namespace Natoshare.Domain.PeopleAndMoney;

// A lightweight record of money actually invested somewhere real (a platform, a
// fund, and so on). Kept separate from the Investment category's own ledger
// movement (which only tracks money leaving the budget for that FixedAccount), so
// the app can compare "how much was set aside" against "how much was actually put
// to work" without the two being the same number by definition.
public class InvestmentLog
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Money Amount { get; init; }

    public DateOnly InvestedOn { get; init; }

    public string Platform { get; set; } = "";

    public string? Note { get; set; }

    public Guid BudgetMonthId { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
}
