namespace Natoshare.Domain.Common;

// Money is how we store every amount in Natoshare. This means allocations, spend,
// savings, deficits, all of it use this type instead of a plain decimal.
// The rule is simple: Money can never be negative. If an amount is short, some other
// part of the code shows that as a deficit, this type will not let you hold a negative
// number at all. We also round every amount to 2 decimal places because we are dealing
// with real currency, not raw numbers.
public readonly record struct Money : IComparable<Money>
{
    // The actual value. Always rounded to 2 decimal places and never negative.
    public decimal Amount { get; }

    // Zero money. Use this instead of typing "new Money(0)" everywhere in the code.
    public static readonly Money Zero = new(0m);

    // Builds a Money value. It rounds the number first, then throws if the result is
    // still negative.
    public Money(decimal amount)
    {
        var rounded = Round(amount);
        if (rounded < 0m)
        {
            throw new NegativeMoneyException(rounded);
        }

        Amount = rounded;
    }

    // Same as calling "new Money(amount)". Some people find this easier to read.
    public static Money From(decimal amount) => new(amount);

    // Rounds a number to 2 decimal places using normal rounding rules.
    // We use this same method everywhere so all our money math matches up.
    private static decimal Round(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    // Adds two Money values together. This can never end up negative because both
    // sides start at zero or more.
    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

    // Multiplies Money by a plain number. We use this when working out a share of an
    // amount, for example turning a percentage into a real amount.
    public static Money operator *(Money a, decimal factor) => new(a.Amount * factor);

    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;

    // Lets us sort or compare Money values, for example to find which category has the
    // biggest allocation.
    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    // Tries to take "amount" away from this Money. If there is not enough here, it does
    // not go negative, it just tells you it was not enough and gives back zero.
    public (bool WasEnough, Money Result) TrySubtract(Money amount)
    {
        if (Amount >= amount.Amount)
        {
            return (true, new Money(Amount - amount.Amount));
        }

        return (false, Zero);
    }

    // Tells you how short "have" is of "need", as a normal positive number.
    // If "have" already covers "need", the deficit is zero. This is how the whole app
    // shows a shortfall without ever using a negative number.
    public static Money Deficit(Money have, Money need)
    {
        return have.Amount >= need.Amount ? Zero : new Money(need.Amount - have.Amount);
    }

    // Picks the bigger of two Money values.
    public static Money Max(Money a, Money b) => a.Amount >= b.Amount ? a : b;

    // Picks the smaller of two Money values.
    public static Money Min(Money a, Money b) => a.Amount <= b.Amount ? a : b;

    // Splits this Money across a list of percentages so the parts always add up to
    // exactly this amount, down to the last kobo or cent.
    // We round each part on its own first. Because of rounding, the parts might not add
    // up to the exact total, so whatever is left over is added to the biggest part.
    // This is the one rule the whole app uses for every income split and allocation.
    public Money[] Allocate(decimal[] percentages)
    {
        if (percentages.Length == 0)
        {
            return [];
        }

        var shares = new decimal[percentages.Length];
        var runningTotal = 0m;

        for (var i = 0; i < percentages.Length; i++)
        {
            shares[i] = Round(Amount * percentages[i] / 100m);
            runningTotal += shares[i];
        }

        var remainder = Round(Amount - runningTotal);
        if (remainder != 0m)
        {
            var biggestIndex = 0;
            for (var i = 1; i < shares.Length; i++)
            {
                if (shares[i] > shares[biggestIndex])
                {
                    biggestIndex = i;
                }
            }

            shares[biggestIndex] += remainder;
        }

        var result = new Money[shares.Length];
        for (var i = 0; i < shares.Length; i++)
        {
            result[i] = new Money(shares[i]);
        }

        return result;
    }

    // Shows the plain number with 2 decimal places, mostly useful for logs.
    // Real screens should format money with the user's own currency and locale, not
    // with this method.
    public override string ToString() => Amount.ToString("F2");
}
