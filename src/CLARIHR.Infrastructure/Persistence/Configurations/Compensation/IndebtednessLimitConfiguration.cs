using CLARIHR.Domain.Compensation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Compensation;

internal sealed class IndebtednessLimitConfiguration : IEntityTypeConfiguration<IndebtednessLimit>
{
    public void Configure(EntityTypeBuilder<IndebtednessLimit> builder)
    {
        builder.ToTable("indebtedness_limits", table =>
            table.HasCheckConstraint(
                "ck_indebtedness_limits__percent",
                "max_percent > 0 and max_percent <= 100"));

        builder.HasKey(item => item.Id).HasName("pk_indebtedness_limits");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.RecurringDeductionTypeCode)
            .HasColumnName("recurring_deduction_type_code")
            .HasMaxLength(IndebtednessLimit.MaxTypeCodeLength);
        builder.Property(item => item.MaxPercent).HasColumnName("max_percent").HasColumnType("numeric(11,8)");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_indebtedness_limits__public_id");

        // One ACTIVE ceiling per type (RF-020). The filter is what lets a type be deactivated and re-added
        // without colliding with its own tombstone.
        builder.HasIndex(item => new { item.TenantId, item.RecurringDeductionTypeCode })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("uq_indebtedness_limits__tenant_type_active");
    }
}
