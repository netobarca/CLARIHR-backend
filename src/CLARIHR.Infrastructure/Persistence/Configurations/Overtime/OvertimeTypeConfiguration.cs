using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using CLARIHR.Domain.Overtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Overtime;

internal sealed class OvertimeTypeConfiguration : IEntityTypeConfiguration<OvertimeType>
{
    public void Configure(EntityTypeBuilder<OvertimeType> builder)
    {
        builder.ToTable("overtime_types", table =>
            table.HasCheckConstraint(
                "ck_overtime_types__default_factor",
                "default_factor > 0"));

        builder.HasKey(type => type.Id)
            .HasName("pk_overtime_types");

        builder.Property(type => type.Id)
            .HasColumnName("id");

        builder.Property(type => type.PublicId)
            .HasColumnName("public_id");

        builder.Property(type => type.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(type => type.Code)
            .HasColumnName("code")
            .HasMaxLength(OvertimeType.MaxCodeLength);

        builder.Property(type => type.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(OvertimeType.MaxCodeLength);

        builder.Property(type => type.Name)
            .HasColumnName("name")
            .HasMaxLength(OvertimeType.MaxNameLength);

        builder.Property(type => type.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(OvertimeType.MaxNameLength);

        builder.Property(type => type.DefaultFactor)
            .HasColumnName("default_factor")
            .HasColumnType("numeric(5,2)");

        builder.Property(type => type.PayrollEffectDescription)
            .HasColumnName("payroll_effect_description")
            .HasMaxLength(OvertimeType.MaxPayrollEffectDescriptionLength);

        builder.Property(type => type.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(type => type.IsActive)
            .HasColumnName("is_active");

        builder.Property(type => type.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(type => type.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(type => type.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(type => type.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_overtime_types__public_id");

        // Filtered unique on the active codes only (a code can be reused after inactivation) — the handlers
        // translate a 23505 on this constraint into a clean 409 conflict.
        builder.HasIndex(type => new { type.TenantId, type.NormalizedCode })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName(OvertimeMasterConstraintNames.OvertimeTypeCodeUnique);

        builder.HasIndex(type => new { type.TenantId, type.IsActive })
            .HasDatabaseName("ix_overtime_types__tenant_active");
    }
}
