using CLARIHR.Domain.Compensation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Compensation;

internal sealed class IncomeTaxWithholdingBracketConfiguration : IEntityTypeConfiguration<IncomeTaxWithholdingBracket>
{
    public void Configure(EntityTypeBuilder<IncomeTaxWithholdingBracket> builder)
    {
        builder.ToTable("income_tax_withholding_brackets", table =>
            table.HasCheckConstraint(
                "ck_income_tax_withholding_brackets__bounds",
                "upper_bound is null or upper_bound >= lower_bound"));

        builder.HasKey(item => item.Id).HasName("pk_income_tax_withholding_brackets");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PayPeriodCode).HasColumnName("pay_period_code").HasMaxLength(40);
        builder.Property(item => item.BracketOrder).HasColumnName("bracket_order");
        builder.Property(item => item.LowerBound).HasColumnName("lower_bound").HasColumnType("numeric(18,2)");
        builder.Property(item => item.UpperBound).HasColumnName("upper_bound").HasColumnType("numeric(18,2)");
        builder.Property(item => item.FixedFee).HasColumnName("fixed_fee").HasColumnType("numeric(18,2)");
        builder.Property(item => item.RatePercent).HasColumnName("rate_percent").HasColumnType("numeric(11,8)");
        builder.Property(item => item.ExcessOver).HasColumnName("excess_over").HasColumnType("numeric(18,2)");
        builder.Property(item => item.EffectiveFromUtc).HasColumnName("effective_from_utc");
        builder.Property(item => item.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_income_tax_withholding_brackets__public_id");

        builder.HasIndex(item => new { item.TenantId, item.PayPeriodCode, item.IsActive, item.BracketOrder })
            .HasDatabaseName("ix_income_tax_withholding_brackets__tenant_period_active_order");
    }
}
