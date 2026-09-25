using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Notifications;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(n => n.Severity).HasConversion<string>().HasMaxLength(10);
        builder.Property(n => n.Title).HasMaxLength(120).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(500).IsRequired();
        builder.Property(n => n.RelatedEntityType).HasMaxLength(50);
        builder.Property(n => n.RelatedEntityId).HasMaxLength(50);

        // The notification centre asks "unread ones for this user" constantly, and
        // the dedup check asks "did we already alert on this today" just as often.
        builder.HasIndex(n => new { n.UserId, n.IsRead });
        builder.HasIndex(n => new { n.UserId, n.Kind, n.RelatedEntityId, n.CreatedAt });
    }
}

public class AlertPreferenceConfiguration : IEntityTypeConfiguration<AlertPreference>
{
    public void Configure(EntityTypeBuilder<AlertPreference> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.ThresholdPercent).HasColumnType("numeric(6,2)");

        builder.HasIndex(p => new { p.UserId, p.Kind }).IsUnique();
    }
}
