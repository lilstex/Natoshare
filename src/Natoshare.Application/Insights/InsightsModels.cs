namespace Natoshare.Application.Insights;

public record TopCategoryDto(Guid CategoryId, string Name, decimal Spent);

// "Up", "Down" or "Flat" compared with last month's spend. Null when there is no
// previous month to compare against yet (a brand new account).
public record SpendingTrendDto(string Direction, decimal? PercentChange);

public record SpendingSummaryResult(
    string PlainEnglish,
    int Year,
    int Month,
    IReadOnlyList<TopCategoryDto> TopCategories,
    SpendingTrendDto Trend);

public record PacingInsightDto(Guid CategoryId, string Name, decimal Projected, string Status, decimal SafeToSpendDaily);
