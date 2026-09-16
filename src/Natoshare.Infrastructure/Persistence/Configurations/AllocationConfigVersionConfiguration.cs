using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Budgeting;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class AllocationConfigVersionConfiguration : IEntityTypeConfiguration<AllocationConfigVersion>
{
    public void Configure(EntityTypeBuilder<AllocationConfigVersion> builder)
    {
        builder.HasKey(v => v.Id);
        builder.Property(v => v.FixedIncomeAmount)
            .HasConversion<MoneyConverter>()
            .HasColumnType("numeric(18,2)");
        builder.Property(v => v.EffectiveFromMonth).HasColumnType("date");
        builder.Property(v => v.Note).HasMaxLength(200);

        builder.HasMany(v => v.Allocations)
            .WithOne()
            .HasForeignKey(a => a.AllocationConfigVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        // We look this up by "which versions does this user have" and "what is active
        // right now" all the time.
        builder.HasIndex(v => new { v.UserId, v.EffectiveFromMonth });
    }
}

public class CategoryAllocationConfiguration : IEntityTypeConfiguration<CategoryAllocation>
{
    public void Configure(EntityTypeBuilder<CategoryAllocation> builder)
    {
        builder.HasKey(a => a.Id);

        // Up to 100.00, two decimal places is all a percentage ever needs.
        builder.Property(a => a.Percentage).HasColumnType("numeric(5,2)");

        builder.HasIndex(a => a.CategoryId);
        builder.HasIndex(a => a.AllocationConfigVersionId);
    }
}
