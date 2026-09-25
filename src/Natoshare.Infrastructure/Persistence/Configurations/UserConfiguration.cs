using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.Identity;

namespace Natoshare.Infrastructure.Persistence.Configurations;

// Sets sensible column sizes for the fields we added on top of IdentityUser. Identity
// itself already configures the login-related columns (email, password hash, and so
// on), we only need to worry about our own additions here.
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(80).IsRequired();
        builder.Property(u => u.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(u => u.CurrencySymbol).HasMaxLength(8).IsRequired();
        builder.Property(u => u.TimeZoneId).HasMaxLength(64).IsRequired();
        builder.Property(u => u.Locale).HasMaxLength(16).IsRequired();
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
    }
}
