using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Admin;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(60);
        builder.Property(s => s.Value).HasMaxLength(500).IsRequired();
    }
}

public class IntegrityCheckRunConfiguration : IEntityTypeConfiguration<IntegrityCheckRun>
{
    public void Configure(EntityTypeBuilder<IntegrityCheckRun> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasIndex(r => r.RanAt);
    }
}
