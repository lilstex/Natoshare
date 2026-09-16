namespace Natoshare.Domain.Budgeting;

// A ready-made split someone can start from, like "50/30/20". Templates belong to
// nobody, they are just reference data, and they only carry percentages, never real
// money, so they work the same no matter what currency the user picked.
public class BudgetTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public List<BudgetTemplateItem> Items { get; set; } = [];
}

public class BudgetTemplateItem
{
    public Guid Id { get; set; }
    public Guid BudgetTemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CategoryKind Kind { get; set; } = CategoryKind.Standard;
    public decimal Percentage { get; set; }
    public int SortOrder { get; set; }
}
