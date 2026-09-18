namespace Natoshare.Domain.Subscriptions;

// A simple on/off switch an admin can flip without a deploy. In v1 there is exactly
// one, "PaymentsEnabled" (always false, see docs/00-plan.md section 5, payments are
// stubbed), but the table is generic so Phase 10's admin feature-flags screen has
// something real to manage.
public class FeatureFlag
{
    public string Key { get; init; } = "";

    public bool Enabled { get; set; }
}
