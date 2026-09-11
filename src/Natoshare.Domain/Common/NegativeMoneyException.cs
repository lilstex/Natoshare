namespace Natoshare.Domain.Common;

// We throw this any time someone tries to make a negative amount of money.
// In Natoshare, money can never go negative. If something is short, we show it as a
// deficit somewhere else in the code, not as negative money.
public sealed class NegativeMoneyException : Exception
{
    // Just builds a simple message that says what amount broke the rule.
    public NegativeMoneyException(decimal amount)
        : base($"Money cannot be negative. Someone tried to create {amount}.")
    {
    }
}
