using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Common;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Key).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ResponseBody).HasColumnType("text");

        // The same key from the same user should only ever produce one result, this
        // is the constraint that actually makes a repeat request safe.
        builder.HasIndex(r => new { r.UserId, r.Key }).IsUnique();
    }
}
