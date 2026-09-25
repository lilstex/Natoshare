using Microsoft.EntityFrameworkCore;
using Natoshare.Application.Ledger;
using Natoshare.Infrastructure.Persistence;

namespace Natoshare.Infrastructure.Ledger;

// The raw, read-only feed behind GET /ledger. Mostly useful for support or someone
// who wants to see exactly what Natoshare recorded.
public class LedgerReadService : ILedgerReadService
{
    private readonly NatoshareDbContext _dbContext;

    public LedgerReadService(NatoshareDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<LedgerEntryDto>> ListAsync(
        Guid userId,
        string? account,
        string? entryType,
        DateOnly? from,
        DateOnly? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Clamped server side so a careless ?pageSize=999999 cannot force a huge
        // read even of the caller's own data (Phase 11's performance pass).
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.LedgerEntries.Where(e => e.UserId == userId);

        if (account is not null)
        {
            query = query.Where(e => e.Account.ToString() == account);
        }

        if (entryType is not null)
        {
            query = query.Where(e => e.EntryType.ToString() == entryType);
        }

        if (from is not null)
        {
            var fromUtc = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(e => e.CreatedAt >= fromUtc);
        }

        if (to is not null)
        {
            var toUtc = new DateTimeOffset(to.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(e => e.CreatedAt <= toUtc);
        }

        var entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);

        return entries.Select(e => new LedgerEntryDto(
            e.Id,
            e.Account.ToString(),
            e.AccountCategoryId,
            e.EntryType.ToString(),
            e.Amount.Amount,
            e.Direction.ToString(),
            e.SourceTxnType.ToString(),
            e.SourceTxnId,
            e.Note,
            e.CreatedAt)).ToList();
    }
}
