namespace Natoshare.Domain.Ledger;

// Points at one exact account: which kind, and which category it belongs to (the
// Flexible Pool is not tied to any category, so its CategoryId is always null).
public readonly record struct AccountRef
{
    public AccountKind Kind { get; }
    public Guid? CategoryId { get; }

    private AccountRef(AccountKind kind, Guid? categoryId)
    {
        Kind = kind;
        CategoryId = categoryId;
    }

    public static AccountRef Category(Guid categoryId) => new(AccountKind.Category, categoryId);

    public static AccountRef CategorySavings(Guid categoryId) => new(AccountKind.CategorySavings, categoryId);

    public static AccountRef External(Guid categoryId) => new(AccountKind.External, categoryId);

    public static AccountRef FlexiblePool() => new(AccountKind.FlexiblePool, null);
}
