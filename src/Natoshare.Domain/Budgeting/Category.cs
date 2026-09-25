namespace Natoshare.Domain.Budgeting;

// A named slice of the budget, like "Feeding" or "Rent". A category's identity stays
// the same over time, its percentage does not live here, it lives on
// CategoryAllocation instead, so changing a percentage never rewrites history.
public class Category
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; } = CategoryKind.Standard;

    // Only really means something for a FixedAccount category, for example
    // "GTBank fixed deposit".
    public string? ExternalAccountLabel { get; set; }

    // Just labels the user can attach to expenses under this category, for example
    // "Groceries" and "Eating out" under Feeding. Not their own budget line.
    public List<string> SubCategories { get; set; } = [];

    public int SortOrder { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
