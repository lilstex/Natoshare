using Microsoft.EntityFrameworkCore;

namespace Natoshare.Infrastructure.Persistence;

// This is Natoshare's connection to the Postgres database.
// Right now it has no tables at all, we are only wiring up the connection in this phase.
// Real tables like Category, LedgerEntry and so on will be added in the phases that
// build those features.
public class NatoshareDbContext : DbContext
{
    // EF Core uses this constructor to pass in the connection settings.
    public NatoshareDbContext(DbContextOptions<NatoshareDbContext> options)
        : base(options)
    {
    }
}
