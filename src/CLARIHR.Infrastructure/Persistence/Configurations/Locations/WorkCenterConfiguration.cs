using CLARIHR.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Locations;

internal sealed class WorkCenterConfiguration : IEntityTypeConfiguration<WorkCenter>
{
    public void Configure(EntityTypeBuilder<WorkCenter> builder)
    {
        builder.ToTable("work_centers");

        builder.HasKey(center => center.Id)
            .HasName("pk_work_centers");

        builder.Property(center => center.Id)
            .HasColumnName("id");

        builder.Property(center => center.PublicId)
            .HasColumnName("public_id");

        builder.Property(center => center.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(center => center.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(center => center.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(center => center.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(center => center.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(center => center.WorkCenterTypeId)
            .HasColumnName("work_center_type_id");

        builder.Property(center => center.LocationGroupId)
            .HasColumnName("location_group_id");

        builder.Property(center => center.Address)
            .HasColumnName("address")
            .HasMaxLength(300);

        builder.Property(center => center.GeoLat)
            .HasColumnName("geo_lat")
            .HasPrecision(9, 6);

        builder.Property(center => center.GeoLong)
            .HasColumnName("geo_long")
            .HasPrecision(9, 6);

        builder.Property(center => center.Phone)
            .HasColumnName("phone")
            .HasMaxLength(50);

        builder.Property(center => center.Email)
            .HasColumnName("email")
            .HasMaxLength(200);

        builder.Property(center => center.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(center => center.IsActive)
            .HasColumnName("is_active");

        builder.Property(center => center.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(center => center.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(center => center.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(center => center.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_work_centers__public_id");

        builder.HasIndex(center => new { center.TenantId, center.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_work_centers__tenant_code");

        builder.HasIndex(center => new { center.TenantId, center.LocationGroupId, center.IsActive })
            .HasDatabaseName("ix_work_centers__tenant_group_active");

        builder.HasIndex(center => new { center.TenantId, center.WorkCenterTypeId, center.IsActive })
            .HasDatabaseName("ix_work_centers__tenant_type_active");

        builder.HasIndex(center => new { center.TenantId, center.NormalizedName })
            .HasDatabaseName("ix_work_centers__tenant_name");

        builder.HasOne<LocationGroup>()
            .WithMany()
            .HasForeignKey(center => center.LocationGroupId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_work_centers__location_groups");

        builder.HasOne<WorkCenterType>()
            .WithMany()
            .HasForeignKey(center => center.WorkCenterTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_work_centers__work_center_types");
    }
}
