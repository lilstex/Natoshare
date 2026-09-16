namespace Natoshare.Application.Ledger;

// The raw, read-only feed behind GET /ledger. Mostly useful for support and for
// someone who wants to see exactly what Natoshare recorded, not something the normal
// app screens are built on.
public interface ILedgerReadService
{
    Task<IReadOnlyList<LedgerEntryDto>> ListAsync(
        Guid userId,
        string? account,
        string? entryType,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
