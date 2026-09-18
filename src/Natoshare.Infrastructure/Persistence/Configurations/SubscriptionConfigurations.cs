using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Subscriptions;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class SubscriptionRecordConfiguration : IEntityTypeConfiguration<SubscriptionRecord>
{
    public void Configure(EntityTypeBuilder<SubscriptionRecord> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Plan).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.BillingCycle).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(10);
        builder.Property(s => s.Reference).HasMaxLength(40).IsRequired();

        builder.HasIndex(s => s.Reference).IsUnique();
        builder.HasIndex(s => new { s.UserId, s.Status });
    }
}

public class PlanConfigConfiguration : IEntityTypeConfiguration<PlanConfig>
{
    public void Configure(EntityTypeBuilder<PlanConfig> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Plan).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(p => p.Plan).IsUnique();
    }
}

public class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.HasKey(f => f.Key);
        builder.Property(f => f.Key).HasMaxLength(60);
    }
}
