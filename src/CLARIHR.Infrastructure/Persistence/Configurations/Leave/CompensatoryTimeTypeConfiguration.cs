using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class CompensatoryTimeTypeConfiguration : IEntityTypeConfiguration<CompensatoryTimeType>
{
    public void Configure(EntityTypeBuilder<CompensatoryTimeType> builder)
    {
        builder.ToTable("compensatory_time_types", table =>
            table.HasCheckConstraint(
                "ck_compensatory_time_types__credit_factor_positive",
                "credit_factor > 0"));

        builder.HasKey(type => type.Id)
            .HasName("pk_compensatory_time_types");

        builder.Property(type => type.Id)
            .HasColumnName("id");

        builder.Property(type => type.PublicId)
            .HasColumnName("public_id");

        builder.Property(type => type.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(type => type.Code)
            .HasColumnName("code")
            .HasMaxLength(CompensatoryTimeType.MaxCodeLength);

        builder.Property(type => type.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(CompensatoryTimeType.MaxCodeLength);

        builder.Property(type => type.Name)
            .HasColumnName("name")
            .HasMaxLength(CompensatoryTimeType.MaxNameLength);

        builder.Property(type => type.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(CompensatoryTimeType.MaxNameLength);

        builder.Property(type => type.OperationCode)
            .HasColumnName("operation_code")
            .HasMaxLength(CompensatoryTimeType.MaxOperationCodeLength);

        builder.Property(type => type.CreditFactor)
            .HasColumnName("credit_factor")
            .HasColumnType("numeric(5,2)");

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
            .HasDatabaseName("uq_compensatory_time_types__public_id");

        // Filtered unique on the active codes only (a code can be reused after inactivation) — the
        // handlers translate a 23505 on this constraint into a clean 409 conflict.
        builder.HasIndex(type => new { type.TenantId, type.NormalizedCode })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName(LeaveMasterConstraintNames.CompensatoryTimeTypeCodeUnique);

        builder.HasIndex(type => new { type.TenantId, type.IsActive })
            .HasDatabaseName("ix_compensatory_time_types__tenant_active");
    }
}
