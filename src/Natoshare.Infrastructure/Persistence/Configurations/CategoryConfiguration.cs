using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Budgeting;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).HasMaxLength(80).IsRequired();
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.ExternalAccountLabel).HasMaxLength(120);

        // A plain Postgres text array, Npgsql knows how to store a List<string> in one
        // of these without needing a separate table.
        builder.Property(c => c.SubCategories).HasColumnType("text[]");

        builder.HasIndex(c => c.UserId);

        // Two categories with the same name, for the same user, would just be
        // confusing, so we do not allow it (case sensitivity is handled in the
        // service layer, Postgres text comparison here is case-sensitive).
        builder.HasIndex(c => new { c.UserId, c.Name }).IsUnique();
    }
}
