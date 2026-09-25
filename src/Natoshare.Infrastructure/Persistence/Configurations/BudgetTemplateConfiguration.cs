using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Budgeting;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class BudgetTemplateConfiguration : IEntityTypeConfiguration<BudgetTemplate>
{
    public void Configure(EntityTypeBuilder<BudgetTemplate> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).HasMaxLength(60).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(300);

        builder.HasMany(t => t.Items)
            .WithOne()
            .HasForeignKey(i => i.BudgetTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class BudgetTemplateItemConfiguration : IEntityTypeConfiguration<BudgetTemplateItem>
{
    public void Configure(EntityTypeBuilder<BudgetTemplateItem> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Name).HasMaxLength(80).IsRequired();
        builder.Property(i => i.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Percentage).HasColumnType("numeric(5,2)");
    }
}
