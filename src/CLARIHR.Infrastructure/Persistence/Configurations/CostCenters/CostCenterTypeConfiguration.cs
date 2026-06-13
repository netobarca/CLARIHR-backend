using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Domain.CostCenters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.CostCenters;

internal sealed class CostCenterTypeConfiguration : IEntityTypeConfiguration<CostCenterType>
{
    public void Configure(EntityTypeBuilder<CostCenterType> builder)
    {
        builder.ToTable("cost_center_types");

        builder.HasKey(type => type.Id)
            .HasName("pk_cost_center_types");

        builder.Property(type => type.Id)
            .HasColumnName("id");

        builder.Property(type => type.PublicId)
            .HasColumnName("public_id");

        builder.Property(type => type.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(type => type.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(type => type.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(type => type.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(type => type.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(type => type.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

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
            .HasDatabaseName("uq_cost_center_types__public_id");

        builder.HasIndex(type => new { type.TenantId, type.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(CostCenterValidationRules.CostCenterTypeCodeUniqueConstraintName);

        builder.HasIndex(type => new { type.TenantId, type.IsActive })
            .HasDatabaseName("ix_cost_center_types__tenant_active");

        builder.HasIndex(type => new { type.TenantId, type.NormalizedName })
            .HasDatabaseName("ix_cost_center_types__tenant_name");
    }
}
