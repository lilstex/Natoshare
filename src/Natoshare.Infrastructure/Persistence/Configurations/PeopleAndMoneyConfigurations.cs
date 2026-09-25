using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Natoshare.Domain.PeopleAndMoney;

namespace Natoshare.Infrastructure.Persistence.Configurations;

public class LoanOutConfiguration : IEntityTypeConfiguration<LoanOut>
{
    public void Configure(EntityTypeBuilder<LoanOut> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.BorrowerName).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Note).HasMaxLength(200);
        builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.LinkedSourceKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(l => l.LentOn).HasColumnType("date");

        builder.HasMany(l => l.Repayments).WithOne().HasForeignKey(r => r.LoanOutId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(l => new { l.UserId, l.Status });
    }
}

public class LoanRepaymentConfiguration : IEntityTypeConfiguration<LoanRepayment>
{
    public void Configure(EntityTypeBuilder<LoanRepayment> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Note).HasMaxLength(200);
        builder.Property(r => r.LinkedDestinationKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(r => r.ReceivedOn).HasColumnType("date");

        builder.HasIndex(r => r.LoanOutId);
    }
}

public class DebtInConfiguration : IEntityTypeConfiguration<DebtIn>
{
    public void Configure(EntityTypeBuilder<DebtIn> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.LenderName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Note).HasMaxLength(200);
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(d => d.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(d => d.BorrowedOn).HasColumnType("date");

        builder.HasMany(d => d.Repayments).WithOne().HasForeignKey(r => r.DebtInId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.UserId, d.Status });
    }
}

public class DebtRepaymentConfiguration : IEntityTypeConfiguration<DebtRepayment>
{
    public void Configure(EntityTypeBuilder<DebtRepayment> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Note).HasMaxLength(200);
        builder.Property(r => r.LinkedSourceKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(r => r.PaidOn).HasColumnType("date");

        builder.HasIndex(r => r.DebtInId);
    }
}

public class PromiseConfiguration : IEntityTypeConfiguration<Promise>
{
    public void Configure(EntityTypeBuilder<Promise> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.PersonName).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(200);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(p => p.MadeOn).HasColumnType("date");

        builder.HasMany(p => p.Redemptions).WithOne().HasForeignKey(r => r.PromiseId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.UserId, p.Status });
    }
}

public class PromiseRedemptionConfiguration : IEntityTypeConfiguration<PromiseRedemption>
{
    public void Configure(EntityTypeBuilder<PromiseRedemption> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Note).HasMaxLength(200);
        builder.Property(r => r.SourceAccountKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(r => r.RedeemedOn).HasColumnType("date");

        builder.HasIndex(r => r.PromiseId);
    }
}

public class InvestmentLogConfiguration : IEntityTypeConfiguration<InvestmentLog>
{
    public void Configure(EntityTypeBuilder<InvestmentLog> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Platform).HasMaxLength(60).IsRequired();
        builder.Property(i => i.Note).HasMaxLength(200);
        builder.Property(i => i.Amount).HasConversion<MoneyConverter>().HasColumnType("numeric(18,2)");
        builder.Property(i => i.InvestedOn).HasColumnType("date");

        builder.HasIndex(i => new { i.UserId, i.InvestedOn });
        builder.HasIndex(i => i.BudgetMonthId);
    }
}
