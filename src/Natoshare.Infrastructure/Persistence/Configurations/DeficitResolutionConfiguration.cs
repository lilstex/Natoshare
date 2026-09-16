using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Transactions;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class DeficitResolutionConfiguration : IEntityTypeConfiguration<DeficitResolution>
{
    public void Configure(EntityTypeBuilder<DeficitResolution> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Method).HasConversion<string>().HasMaxLength(30);
        builder.Property(d => d.Note).HasMaxLength(200);
        builder.Property(d => d.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(d => d.ResolvedOn).HasColumnType("date");

        builder.HasIndex(d => new { d.BudgetMonthId, d.CategoryId });
    }
}
