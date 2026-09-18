namespace Natoshare.Domain.PeopleAndMoney;

// Pure math for working out whether a loan, debt or promise is fully settled yet,
// shared by every place that records a repayment or a redemption so the same
// rounding and edge cases are handled exactly once.
public static class ObligationStatusCalculator
{
    // WrittenOff is a manual, sticky choice, a new repayment after that never
    // silently brings it back to Outstanding or Repaid on its own.
    public static LoanOutStatus ComputeLoanStatus(decimal amount, decimal totalRepaid, LoanOutStatus currentStatus)
    {
        if (currentStatus == LoanOutStatus.WrittenOff)
        {
            return LoanOutStatus.WrittenOff;
        }

        if (totalRepaid >= amount)
        {
            return LoanOutStatus.Repaid;
        }

        return totalRepaid > 0m ? LoanOutStatus.PartiallyRepaid : LoanOutStatus.Outstanding;
    }

    public static DebtInStatus ComputeDebtStatus(decimal amount, decimal totalRepaid)
    {
        if (totalRepaid >= amount)
        {
            return DebtInStatus.Repaid;
        }

        return totalRepaid > 0m ? DebtInStatus.PartiallyRepaid : DebtInStatus.Outstanding;
    }

    // Cancelled is a manual, sticky choice, same reasoning as WrittenOff above.
    public static PromiseStatus ComputePromiseStatus(decimal amount, decimal totalRedeemed, PromiseStatus currentStatus)
    {
        if (currentStatus == PromiseStatus.Cancelled)
        {
            return PromiseStatus.Cancelled;
        }

        if (totalRedeemed >= amount)
        {
            return PromiseStatus.Redeemed;
        }

        return totalRedeemed > 0m ? PromiseStatus.PartiallyRedeemed : PromiseStatus.Open;
    }
}
