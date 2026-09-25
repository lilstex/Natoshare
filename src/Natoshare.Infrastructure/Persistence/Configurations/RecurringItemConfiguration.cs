using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Planning;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class RecurringItemConfiguration : IEntityTypeConfiguration<RecurringItem>
{
    public void Configure(EntityTypeBuilder<RecurringItem> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.IncomeType).HasConversion<string>().HasMaxLength(15);
        builder.Property(r => r.Cadence).HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.Mode).HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.Description).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(r => r.NextRunOn).HasColumnType("date");
        builder.Property(r => r.LastPostedOn).HasColumnType("date");

        builder.HasIndex(r => new { r.UserId, r.IsActive, r.NextRunOn });
    }
}
