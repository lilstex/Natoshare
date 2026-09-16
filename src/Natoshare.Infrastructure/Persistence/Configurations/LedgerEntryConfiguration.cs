using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Ledger;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Account).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.EntryType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Direction).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.SourceTxnType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Note).HasMaxLength(200);

        builder.Property(e => e.Amount)
            .HasConversion<MoneyConverter>()
            .HasColumnType("numeric(18,2)");

        // Every balance screen asks "what has this account got", and every reversal
        // asks "what did this transaction post", these are the two questions the
        // whole ledger exists to answer fast.
        builder.HasIndex(e => new { e.UserId, e.Account, e.AccountCategoryId });
        builder.HasIndex(e => new { e.SourceTxnType, e.SourceTxnId });
        builder.HasIndex(e => e.BudgetMonthId);
    }
}
