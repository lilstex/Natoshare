namespace Natoshare.Domain.Budgeting;

// Works out which AllocationConfigVersion actually applied in a given month. This is
// plain logic with no database in it, so we can test it on its own.
public static class AllocationResolver
{
    // The version that applies to "month" is the one with the latest EffectiveFromMonth
    // that is not after "month", ignoring any version that got superseded before it was
    // ever used. If nothing applies yet (for example, the user has not finished
    // onboarding), this gives back null.
    public static AllocationConfigVersion? ResolveActiveVersion(
        IEnumerable<AllocationConfigVersion> versions,
        DateOnly month)
    {
        return versions
            .Where(v => !v.IsSuperseded && v.EffectiveFromMonth <= month)
            .OrderByDescending(v => v.EffectiveFromMonth)
            .ThenByDescending(v => v.CreatedAt)
            .FirstOrDefault();
    }
}
