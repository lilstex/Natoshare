using FluentValidation;

namespace Natoshare.Application.Admin;

public class SuspendUserRequestValidator : AbstractValidator<SuspendUserRequest>
{
    public SuspendUserRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class AdjustPlanRequestValidator : AbstractValidator<AdjustPlanRequest>
{
    public AdjustPlanRequestValidator()
    {
        RuleFor(x => x.Plan).Must(p => p is null or "Free" or "Pro").WithMessage("Plan must be 'Free' or 'Pro'.");
        RuleFor(x => x).Must(x => x.Plan is not null || x.TrialEndsAt is not null)
            .WithMessage("Provide at least a plan or a new trial end date.");
    }
}

// The typed-confirm requirement from docs/04-admin-app.md section 4: an admin has to
// type the account's own email back to us before we hard-delete it, exactly like
// GitHub's "type the repo name to delete it" pattern, so a stray click cannot wipe
// someone's account.
public class HardDeleteUserRequestValidator : AbstractValidator<HardDeleteUserRequest>
{
    public HardDeleteUserRequestValidator()
    {
        RuleFor(x => x.ConfirmText).NotEmpty();
    }
}

public class PatchPlanConfigRequestValidator : AbstractValidator<PatchPlanConfigRequest>
{
    public PatchPlanConfigRequestValidator()
    {
        RuleFor(x => x.MaxCategories).GreaterThan(0).When(x => x.MaxCategories is not null);
        RuleFor(x => x.HistoryWindowDays).GreaterThan(0).When(x => x.HistoryWindowDays is not null);
    }
}

public class PatchFeatureFlagRequestValidator : AbstractValidator<PatchFeatureFlagRequest>
{
}

public class PatchSettingRequestValidator : AbstractValidator<PatchSettingRequest>
{
    public PatchSettingRequestValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(500);
    }
}
