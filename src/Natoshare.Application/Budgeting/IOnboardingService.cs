namespace Natoshare.Application.Budgeting;

// The one-time wizard: currency, timezone, income and categories, all in one go.
public interface IOnboardingService
{
    Task<OnboardingStateResult> GetStateAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BudgetTemplateDto>> GetTemplatesAsync(CancellationToken cancellationToken = default);

    Task<AllocationVersionResult> CompleteAsync(Guid userId, OnboardingCompleteRequest request, CancellationToken cancellationToken = default);
}
