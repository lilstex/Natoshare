using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Transactions;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(i => i.Description).HasMaxLength(200).IsRequired();
        builder.Property(i => i.TotalAmount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(i => i.OccurredOn).HasColumnType("date");

        builder.HasMany(i => i.Splits)
            .WithOne()
            .HasForeignKey(s => s.IncomeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => new { i.UserId, i.OccurredOn });
        builder.HasIndex(i => i.BudgetMonthId);
    }
}

public class IncomeSplitConfiguration : IEntityTypeConfiguration<IncomeSplit>
{
    public void Configure(EntityTypeBuilder<IncomeSplit> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.HasIndex(s => s.CategoryId);
    }
}
