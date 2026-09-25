namespace Natoshare.Domain.Budgeting;

// How much of the fixed income one category gets, as a percentage, for one
// AllocationConfigVersion. The percentages of every active category on the same
// version must add up to exactly 100.
public class CategoryAllocation
{
    public Guid Id { get; set; }
    public Guid AllocationConfigVersionId { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Percentage { get; set; }
}
