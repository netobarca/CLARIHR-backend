using CLARIHR.Application.Features.EmployeeRelations.Common;
using CLARIHR.Domain.EmployeeRelations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.EmployeeRelations;

internal sealed class DisciplinaryActionCauseConfiguration : IEntityTypeConfiguration<DisciplinaryActionCause>
{
    public void Configure(EntityTypeBuilder<DisciplinaryActionCause> builder)
    {
        builder.ToTable("disciplinary_action_causes");

        builder.HasKey(cause => cause.Id)
            .HasName("pk_disciplinary_action_causes");

        builder.Property(cause => cause.Id)
            .HasColumnName("id");

        builder.Property(cause => cause.PublicId)
            .HasColumnName("public_id");

        builder.Property(cause => cause.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(cause => cause.Code)
            .HasColumnName("code")
            .HasMaxLength(DisciplinaryActionCause.MaxCodeLength);

        builder.Property(cause => cause.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(DisciplinaryActionCause.MaxCodeLength);

        builder.Property(cause => cause.Name)
            .HasColumnName("name")
            .HasMaxLength(DisciplinaryActionCause.MaxNameLength);

        builder.Property(cause => cause.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(DisciplinaryActionCause.MaxNameLength);

        builder.Property(cause => cause.DeductionConceptTypeCode)
            .HasColumnName("deduction_concept_type_code")
            .HasMaxLength(DisciplinaryActionCause.MaxDeductionConceptTypeCodeLength);

        builder.Property(cause => cause.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(cause => cause.IsActive)
            .HasColumnName("is_active");

        builder.Property(cause => cause.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(cause => cause.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(cause => cause.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(cause => cause.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_disciplinary_action_causes__public_id");

        // Filtered unique on the active codes only (a code can be reused after inactivation) — the
        // handlers translate a 23505 on this constraint into a clean 409 conflict.
        builder.HasIndex(cause => new { cause.TenantId, cause.NormalizedCode })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName(EmployeeRelationsMasterConstraintNames.DisciplinaryActionCauseCodeUnique);

        builder.HasIndex(cause => new { cause.TenantId, cause.IsActive })
            .HasDatabaseName("ix_disciplinary_action_causes__tenant_active");
    }
}
