using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class IncapacityTypeConfiguration : IEntityTypeConfiguration<IncapacityType>
{
    public void Configure(EntityTypeBuilder<IncapacityType> builder)
    {
        builder.ToTable("incapacity_types");

        builder.HasKey(type => type.Id)
            .HasName("pk_incapacity_types");

        builder.Property(type => type.Id)
            .HasColumnName("id");

        builder.Property(type => type.PublicId)
            .HasColumnName("public_id");

        builder.Property(type => type.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(type => type.Code)
            .HasColumnName("code")
            .HasMaxLength(IncapacityType.MaxCodeLength);

        builder.Property(type => type.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(IncapacityType.MaxCodeLength);

        builder.Property(type => type.Name)
            .HasColumnName("name")
            .HasMaxLength(IncapacityType.MaxNameLength);

        builder.Property(type => type.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(IncapacityType.MaxNameLength);

        builder.Property(type => type.DeductionTypeText)
            .HasColumnName("deduction_type_text")
            .HasMaxLength(IncapacityType.MaxDeductionTypeTextLength);

        builder.Property(type => type.IncomeTypeText)
            .HasColumnName("income_type_text")
            .HasMaxLength(IncapacityType.MaxIncomeTypeTextLength);

        builder.Property(type => type.AppliesToWorkAccident)
            .HasColumnName("applies_to_work_accident");

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
            .HasDatabaseName("uq_incapacity_types__public_id");

        builder.HasIndex(type => new { type.TenantId, type.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(LeaveMasterConstraintNames.IncapacityTypeCodeUnique);
    }
}
