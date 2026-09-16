using Natoshare.Domain.Common;

namespace Natoshare.Domain.Budgeting;

// A dated snapshot of "how much I earn and how it is split". Every time the user
// changes their income or their category percentages, we make a new version instead
// of editing the old one, so we always know what the rules were for any given month.
public class AllocationConfigVersion
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Money FixedIncomeAmount { get; set; }

    // The first calendar month this version applies to. We always store the 1st of
    // the month here, the day itself does not mean anything.
    public DateOnly EffectiveFromMonth { get; set; }

    public string? Note { get; set; }

    // Set if a newer version came along and took over before this one ever got used
    // for real. A superseded version is never picked when we resolve "what applied in
    // month X".
    public DateTimeOffset? SupersededAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<CategoryAllocation> Allocations { get; set; } = [];

    public bool IsSuperseded => SupersededAt is not null;
}
