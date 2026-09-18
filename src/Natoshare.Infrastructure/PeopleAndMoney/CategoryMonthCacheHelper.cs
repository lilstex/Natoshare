using Microsoft.EntityFrameworkCore;
using Natoshare.Domain.Common;
using Natoshare.Domain.Ledger;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.PeopleAndMoney;

// A Category account's Funded amount is cached on CategoryMonth for speed (unlike
// CategorySavings, FlexiblePool and External, which are always worked out live from
// the ledger). Loans, debts and promises can all move money straight in or out of a
// plain Category account, so whenever one of them does, this is what keeps that
// cache in step with the ledger entry being posted alongside it.
internal static class CategoryMonthCacheHelper
{
    public static async Task AdjustAllocatedAsync(
        NatoshareDbContext dbContext, Guid budgetMonthId, AccountRef account, Money amount, bool isCredit, CancellationToken cancellationToken)
    {
        if (account.Kind != AccountKind.Category || account.CategoryId is null)
        {
            return;
        }

        var categoryMonth = await dbContext.CategoryMonths
            .FirstOrDefaultAsync(cm => cm.BudgetMonthId == budgetMonthId && cm.CategoryId == account.CategoryId, cancellationToken);

        if (categoryMonth is null)
        {
            return;
        }

        categoryMonth.AllocatedAmount = isCredit
            ? new Money(categoryMonth.AllocatedAmount.Amount + amount.Amount)
            : new Money(Math.Max(0m, categoryMonth.AllocatedAmount.Amount - amount.Amount));
    }
}
