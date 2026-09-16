using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Transactions;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
        builder.Property(e => e.SubCategory).HasMaxLength(60);
        builder.Property(e => e.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(e => e.OccurredOn).HasColumnType("date");

        builder.HasMany(e => e.ExpenseTags)
            .WithOne()
            .HasForeignKey(t => t.ExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.UserId, e.OccurredOn });
        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.BudgetMonthId);
    }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(40).IsRequired();
        builder.HasIndex(t => new { t.UserId, t.Name }).IsUnique();
    }
}

public class ExpenseTagConfiguration : IEntityTypeConfiguration<ExpenseTag>
{
    public void Configure(EntityTypeBuilder<ExpenseTag> builder)
    {
        builder.HasKey(t => new { t.ExpenseId, t.TagId });
        builder.HasIndex(t => t.TagId);
    }
}
