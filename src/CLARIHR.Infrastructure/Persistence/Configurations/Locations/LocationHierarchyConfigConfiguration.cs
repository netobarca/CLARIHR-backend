using CLARIHR.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Locations;

internal sealed class LocationHierarchyConfigConfiguration : IEntityTypeConfiguration<LocationHierarchyConfig>
{
    public void Configure(EntityTypeBuilder<LocationHierarchyConfig> builder)
    {
        builder.ToTable("location_hierarchy_configs");

        builder.HasKey(config => config.Id)
            .HasName("pk_location_hierarchy_configs");

        builder.Property(config => config.Id)
            .HasColumnName("id");

        builder.Property(config => config.PublicId)
            .HasColumnName("public_id");

        builder.Property(config => config.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(config => config.IsMultiLevel)
            .HasColumnName("is_multi_level");

        builder.Property(config => config.DefaultGroupCode)
            .HasColumnName("default_group_code")
            .HasMaxLength(50);

        builder.Property(config => config.DefaultGroupName)
            .HasColumnName("default_group_name")
            .HasMaxLength(150);

        builder.Property(config => config.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(config => config.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(config => config.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(config => config.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_location_hierarchy_configs__public_id");

        builder.HasIndex(config => config.TenantId)
            .IsUnique()
            .HasDatabaseName("uq_location_hierarchy_configs__tenant_id");
    }
}
