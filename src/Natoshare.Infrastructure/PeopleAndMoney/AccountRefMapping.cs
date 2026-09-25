using Natoshare.Application.Ledger;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// Converts between the wire-shaped AccountRefInput (a plain "kind" string, used by
// requests and DTOs) and the domain's AccountRef, the same two shapes
// ReallocationService already converts between, just shared here since loans, debts
// and promises all need the exact same conversion.
internal static class AccountRefMapping
{
    public static AccountRef ToAccountRef(AccountRefInput input) => input.Kind switch
    {
        "Category" => AccountRef.Category(input.CategoryId!.Value),
        "CategorySavings" => AccountRef.CategorySavings(input.CategoryId!.Value),
        "FlexiblePool" => AccountRef.FlexiblePool(),
        _ => throw new ValidationFailedException(new Dictionary<string, string[]> { ["kind"] = [$"'{input.Kind}' is not a valid account kind."] }),
    };

    public static AccountRefInput? ToInput(AccountKind? kind, Guid? categoryId) =>
        kind is null ? null : new AccountRefInput(kind.Value.ToString(), categoryId);
}
