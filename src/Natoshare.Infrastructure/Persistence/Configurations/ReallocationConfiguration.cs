using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Transactions;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class ReallocationConfiguration : IEntityTypeConfiguration<Reallocation>
{
    public void Configure(EntityTypeBuilder<Reallocation> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.FromAccountKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ToAccountKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Note).HasMaxLength(200);
        builder.Property(r => r.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(r => r.OccurredOn).HasColumnType("date");

        builder.HasIndex(r => new { r.UserId, r.OccurredOn });
        builder.HasIndex(r => r.BudgetMonthId);
    }
}
