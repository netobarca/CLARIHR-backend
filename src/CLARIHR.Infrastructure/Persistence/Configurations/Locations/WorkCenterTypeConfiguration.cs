using CLARIHR.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Locations;

internal sealed class WorkCenterTypeConfiguration : IEntityTypeConfiguration<WorkCenterType>
{
    public void Configure(EntityTypeBuilder<WorkCenterType> builder)
    {
        builder.ToTable("work_center_types");

        builder.HasKey(type => type.Id)
            .HasName("pk_work_center_types");

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

        builder.Property(type => type.RequiresAddress)
            .HasColumnName("requires_address");

        builder.Property(type => type.RequiresGeo)
            .HasColumnName("requires_geo");

        builder.Property(type => type.AllowsBiometric)
            .HasColumnName("allows_biometric");

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
            .HasDatabaseName("uq_work_center_types__public_id");

        builder.HasIndex(type => new { type.TenantId, type.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_work_center_types__tenant_code");

        builder.HasIndex(type => new { type.TenantId, type.IsActive })
            .HasDatabaseName("ix_work_center_types__tenant_active");

        builder.HasIndex(type => new { type.TenantId, type.NormalizedName })
            .HasDatabaseName("ix_work_center_types__tenant_name");
    }
}
