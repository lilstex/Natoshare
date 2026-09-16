using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Natoshare.Domain.Audit;
using Natoshare.Domain.Budgeting;
using Natoshare.Domain.Identity;

namespace Natoshare.Infrastructure.Persistence;

// This is Natoshare's connection to the Postgres database.
// It builds on IdentityDbContext, which already gives us the tables ASP.NET Core
// Identity needs (users, roles, and so on). We add our own tables on top, like
// RefreshToken and AuditEvent. More tables (the ledger, and so on) get added in the
// phases that build those features.
public class NatoshareDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    public NatoshareDbContext(DbContextOptions<NatoshareDbContext> options)
        : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<DataExportRequest> DataExportRequests => Set<DataExportRequest>();

    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<AllocationConfigVersion> AllocationConfigVersions => Set<AllocationConfigVersion>();

    public DbSet<CategoryAllocation> CategoryAllocations => Set<CategoryAllocation>();

    public DbSet<BudgetTemplate> BudgetTemplates => Set<BudgetTemplate>();

    public DbSet<BudgetTemplateItem> BudgetTemplateItems => Set<BudgetTemplateItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NatoshareDbContext).Assembly);
    }
}
