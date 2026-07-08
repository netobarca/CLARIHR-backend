using CLARIHR.Application.Features.EmployeeRelations.Common;
using CLARIHR.Domain.EmployeeRelations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.EmployeeRelations;

internal sealed class RecognitionTypeConfiguration : IEntityTypeConfiguration<RecognitionType>
{
    public void Configure(EntityTypeBuilder<RecognitionType> builder)
    {
        builder.ToTable("recognition_types");

        builder.HasKey(type => type.Id)
            .HasName("pk_recognition_types");

        builder.Property(type => type.Id)
            .HasColumnName("id");

        builder.Property(type => type.PublicId)
            .HasColumnName("public_id");

        builder.Property(type => type.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(type => type.Code)
            .HasColumnName("code")
            .HasMaxLength(RecognitionType.MaxCodeLength);

        builder.Property(type => type.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(RecognitionType.MaxCodeLength);

        builder.Property(type => type.Name)
            .HasColumnName("name")
            .HasMaxLength(RecognitionType.MaxNameLength);

        builder.Property(type => type.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(RecognitionType.MaxNameLength);

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
            .HasDatabaseName("uq_recognition_types__public_id");

        // Filtered unique on the active codes only (a code can be reused after inactivation) — the
        // handlers translate a 23505 on this constraint into a clean 409 conflict.
        builder.HasIndex(type => new { type.TenantId, type.NormalizedCode })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName(EmployeeRelationsMasterConstraintNames.RecognitionTypeCodeUnique);

        builder.HasIndex(type => new { type.TenantId, type.IsActive })
            .HasDatabaseName("ix_recognition_types__tenant_active");
    }
}
