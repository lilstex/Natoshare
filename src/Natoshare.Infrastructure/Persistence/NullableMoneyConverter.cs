using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Natoshare.Domain.Common;

namespace Natoshare.Infrastructure.Persistence;

// Same idea as MoneyConverter, but for the columns that are allowed to have no value
// at all yet, like a category's external transfer amount before anyone confirms one.
public class NullableMoneyConverter : ValueConverter<Money?, decimal?>
{
    public NullableMoneyConverter()
        : base(money => money.HasValue ? money.Value.Amount : null, value => value.HasValue ? new Money(value.Value) : null)
    {
    }
}
