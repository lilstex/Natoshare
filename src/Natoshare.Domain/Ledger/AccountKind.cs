namespace Natoshare.Domain.Ledger;

// Every place money can sit in Natoshare. A category's day-to-day spending money, its
// own savings pile, the shared Flexible Pool, or money already moved out to an
// external account (like a fixed deposit).
public enum AccountKind
{
    Category,
    CategorySavings,
    FlexiblePool,
    External,
}
