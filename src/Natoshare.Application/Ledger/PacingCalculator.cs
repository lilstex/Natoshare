using Natoshare.Domain.Budgeting;

namespace Natoshare.Application.Ledger;

// Works out whether a category is on track, overspending, or already in deficit, and
// how much is safe to still spend each day. Shared by the expense response and the
// balances screen so the two never disagree with each other.
public static class PacingCalculator
{
    public record Result(decimal Projected, string Status, decimal SafeToSpendDaily);

    public static Result Compute(CategoryMonth categoryMonth, int year, int month, DateOnly today)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var isCurrentMonth = today.Year == year && today.Month == month;
        var daysElapsed = isCurrentMonth ? today.Day : daysInMonth;
        var daysRemaining = Math.Max(1, daysInMonth - daysElapsed + (isCurrentMonth ? 1 : 0));

        var funded = categoryMonth.Funded().Amount;
        var spent = categoryMonth.SpentAmount.Amount;
        var projected = daysElapsed > 0 ? Math.Round(spent / daysElapsed * daysInMonth, 2) : 0m;

        var status = categoryMonth.Deficit().Amount > 0m ? "InDeficit" : projected > funded ? "OverPace" : "OnTrack";
        var safeToSpendDaily = Math.Round(Math.Max(0m, (funded - spent) / daysRemaining), 2);

        return new Result(projected, status, safeToSpendDaily);
    }
}
