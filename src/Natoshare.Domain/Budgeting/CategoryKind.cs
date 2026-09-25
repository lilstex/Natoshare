namespace Natoshare.Domain.Budgeting;

public enum CategoryKind
{
    // Money is spent from this one directly. Whatever is left over becomes savings.
    Standard,

    // The allocation is meant to leave for an outside account (rent, investment, and
    // so on). The user confirms each month that the transfer actually happened.
    FixedAccount
}
