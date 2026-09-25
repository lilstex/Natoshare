using FluentValidation;

namespace Natoshare.Application.Subscriptions;

public class UpgradeSubscriptionRequestValidator : AbstractValidator<UpgradeSubscriptionRequest>
{
    public UpgradeSubscriptionRequestValidator()
    {
        RuleFor(x => x.Plan).Must(p => p == "Pro").WithMessage("Only upgrading to 'Pro' is supported right now.");
        RuleFor(x => x.BillingCycle).Must(b => b is "monthly" or "annual").WithMessage("billingCycle must be 'monthly' or 'annual'.");
    }
}
