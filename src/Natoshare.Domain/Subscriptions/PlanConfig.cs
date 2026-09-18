namespace Natoshare.Domain.Subscriptions;

// The actual numbers behind each plan (see docs/00-plan.md section 5's capability
// table), kept in the database instead of hardcoded so an admin can tune them
// later without a deploy. Seeded once by ReferenceDataSeeder, one row per PlanTier.
public class PlanConfig
{
    public Guid Id { get; init; }

    public PlanTier Plan { get; init; }

    // Null means unlimited.
    public int? MaxCategories { get; set; }

    // Null means unlimited (full history).
    public int? HistoryWindowDays { get; set; }

    public bool SinkingFundEnabled { get; set; }

    public bool DeficitCoverFromSavingsEnabled { get; set; }

    public bool RecurringItemsEnabled { get; set; }

    public bool ExportEnabled { get; set; }
}
