using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Budgeting;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class BudgetMonthConfiguration : IEntityTypeConfiguration<BudgetMonth>
{
    public void Configure(EntityTypeBuilder<BudgetMonth> builder)
    {
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(m => m.FixedIncomeSnapshot)
            .HasConversion<MoneyConverter>()
            .HasColumnType("numeric(18,2)");

        builder.HasMany(m => m.CategoryMonths)
            .WithOne()
            .HasForeignKey(cm => cm.BudgetMonthId)
            .OnDelete(DeleteBehavior.Cascade);

        // A user only ever has one month row per calendar month.
        builder.HasIndex(m => new { m.UserId, m.Year, m.Month }).IsUnique();
    }
}

public class CategoryMonthConfiguration : IEntityTypeConfiguration<CategoryMonth>
{
    public void Configure(EntityTypeBuilder<CategoryMonth> builder)
    {
        builder.HasKey(cm => cm.Id);

        builder.Property(cm => cm.AllocatedAmount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.CarriedInSavings).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.CarriedInDeficit).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.SpentAmount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.CoveredAmount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");

        builder.Property(cm => cm.ExternalTransferAmount).HasConversion<NullableMoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.SavedThisMonth).HasConversion<NullableMoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.DeficitAtClose).HasConversion<NullableMoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.CarriedOutSavings).HasConversion<NullableMoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(cm => cm.CarriedOutDeficit).HasConversion<NullableMoneyConverter>().HasColumnType("numeric(18,2)");

        builder.Property(cm => cm.DeficitResolvedVia).HasConversion<string>().HasMaxLength(30);

        builder.HasIndex(cm => new { cm.BudgetMonthId, cm.CategoryId }).IsUnique();
        builder.HasIndex(cm => cm.CategoryId);
    }
}
