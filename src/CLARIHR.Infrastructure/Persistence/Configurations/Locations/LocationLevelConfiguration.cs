using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Locations;

internal sealed class LocationLevelConfiguration : IEntityTypeConfiguration<LocationLevel>
{
    public void Configure(EntityTypeBuilder<LocationLevel> builder)
    {
        builder.ToTable("location_levels");

        builder.HasKey(level => level.Id)
            .HasName("pk_location_levels");

        builder.Property(level => level.Id)
            .HasColumnName("id");

        builder.Property(level => level.PublicId)
            .HasColumnName("public_id");

        builder.Property(level => level.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(level => level.LevelOrder)
            .HasColumnName("level_order");

        builder.Property(level => level.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(100);

        builder.Property(level => level.IsActive)
            .HasColumnName("is_active");

        builder.Property(level => level.IsRequired)
            .HasColumnName("is_required");

        builder.Property(level => level.AllowsWorkCenters)
            .HasColumnName("allows_work_centers");

        builder.Property(level => level.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(level => level.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(level => level.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(level => level.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_location_levels__public_id");

        builder.HasIndex(level => new { level.TenantId, level.LevelOrder })
            .IsUnique()
            .HasDatabaseName(LocationValidationRules.LevelOrderUniqueConstraintName);

        builder.HasIndex(level => new { level.TenantId, level.IsActive, level.LevelOrder })
            .HasDatabaseName("ix_location_levels__tenant_active_order");
    }
}
