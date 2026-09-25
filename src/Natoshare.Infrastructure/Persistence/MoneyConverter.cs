using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Natoshare.Domain.Common;

namespace Natoshare.Infrastructure.Persistence;

// Lets EF Core store a Money value as a plain decimal column, and turn it back into a
// real Money value (so it still rejects negatives) whenever we read it out.
public class MoneyConverter : ValueConverter<Money, decimal>
{
    public MoneyConverter()
        : base(money => money.Amount, value => new Money(value))
    {
    }
}
