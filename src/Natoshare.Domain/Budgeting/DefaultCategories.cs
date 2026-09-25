namespace Natoshare.Domain.Budgeting;

// One category name, kind and percentage, used both to seed a new account's
// categories and to build the "Everyday" template. Keeping this list in one place
// means the two can never drift apart.
public record DefaultCategoryDefinition(string Name, CategoryKind Kind, decimal Percentage);

public static class DefaultCategories
{
    // See docs/00-plan.md and docs/01-domain-model.md, this exact set and split is the
    // one Natoshare gives every new account.
    public static readonly IReadOnlyList<DefaultCategoryDefinition> Items =
    [
        new("Rent", CategoryKind.FixedAccount, 25m),
        new("Feeding", CategoryKind.Standard, 25m),
        new("Transportation", CategoryKind.Standard, 15m),
        new("Utility", CategoryKind.Standard, 10m),
        new("Subscription", CategoryKind.Standard, 5m),
        new("Investment", CategoryKind.FixedAccount, 20m),
    ];
}
