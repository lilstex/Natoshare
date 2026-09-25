namespace Natoshare.Domain.Subscriptions;

// The two real plans in v1. A trial behaves exactly like Pro for as long as it
// lasts (see IEntitlementService), it is not a third tier of its own.
public enum PlanTier
{
    Free,
    Pro,
}
