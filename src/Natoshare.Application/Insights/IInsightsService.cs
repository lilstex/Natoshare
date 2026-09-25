namespace Natoshare.Application.Insights;

public interface IInsightsService
{
    Task<SpendingSummaryResult> GetSpendingSummaryAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PacingInsightDto>> GetPacingAsync(Guid userId, CancellationToken cancellationToken = default);
}
