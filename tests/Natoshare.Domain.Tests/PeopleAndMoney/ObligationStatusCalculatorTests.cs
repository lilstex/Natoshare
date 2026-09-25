using FluentAssertions;
using Natoshare.Domain.PeopleAndMoney;
using Xunit;

namespace Natoshare.Domain.Tests.PeopleAndMoney;

public class ObligationStatusCalculatorTests
{
    [Fact]
    public void A_loan_with_nothing_repaid_yet_is_outstanding()
    {
        ObligationStatusCalculator.ComputeLoanStatus(20000m, 0m, LoanOutStatus.Outstanding).Should().Be(LoanOutStatus.Outstanding);
    }

    [Fact]
    public void A_loan_with_some_but_not_all_repaid_is_partially_repaid()
    {
        ObligationStatusCalculator.ComputeLoanStatus(20000m, 5000m, LoanOutStatus.Outstanding).Should().Be(LoanOutStatus.PartiallyRepaid);
    }

    [Fact]
    public void A_loan_repaid_in_full_is_repaid()
    {
        ObligationStatusCalculator.ComputeLoanStatus(20000m, 20000m, LoanOutStatus.PartiallyRepaid).Should().Be(LoanOutStatus.Repaid);
    }

    [Fact]
    public void A_loan_repaid_more_than_the_original_amount_is_still_just_repaid()
    {
        ObligationStatusCalculator.ComputeLoanStatus(20000m, 25000m, LoanOutStatus.PartiallyRepaid).Should().Be(LoanOutStatus.Repaid);
    }

    [Fact]
    public void WrittenOff_is_sticky_and_never_recomputes_back_on_a_new_repayment()
    {
        ObligationStatusCalculator.ComputeLoanStatus(20000m, 5000m, LoanOutStatus.WrittenOff).Should().Be(LoanOutStatus.WrittenOff);
    }

    [Fact]
    public void A_debt_with_nothing_repaid_yet_is_outstanding()
    {
        ObligationStatusCalculator.ComputeDebtStatus(15000m, 0m).Should().Be(DebtInStatus.Outstanding);
    }

    [Fact]
    public void A_debt_with_some_but_not_all_repaid_is_partially_repaid()
    {
        ObligationStatusCalculator.ComputeDebtStatus(15000m, 5000m).Should().Be(DebtInStatus.PartiallyRepaid);
    }

    [Fact]
    public void A_debt_repaid_in_full_is_repaid()
    {
        ObligationStatusCalculator.ComputeDebtStatus(15000m, 15000m).Should().Be(DebtInStatus.Repaid);
    }

    [Fact]
    public void A_promise_with_nothing_redeemed_yet_is_open()
    {
        ObligationStatusCalculator.ComputePromiseStatus(10000m, 0m, PromiseStatus.Open).Should().Be(PromiseStatus.Open);
    }

    [Fact]
    public void A_promise_with_some_but_not_all_redeemed_is_partially_redeemed()
    {
        ObligationStatusCalculator.ComputePromiseStatus(10000m, 4000m, PromiseStatus.Open).Should().Be(PromiseStatus.PartiallyRedeemed);
    }

    [Fact]
    public void A_promise_redeemed_in_full_is_redeemed()
    {
        ObligationStatusCalculator.ComputePromiseStatus(10000m, 10000m, PromiseStatus.PartiallyRedeemed).Should().Be(PromiseStatus.Redeemed);
    }

    [Fact]
    public void Cancelled_is_sticky_and_never_recomputes_back_on_a_new_redemption()
    {
        ObligationStatusCalculator.ComputePromiseStatus(10000m, 4000m, PromiseStatus.Cancelled).Should().Be(PromiseStatus.Cancelled);
    }
}
