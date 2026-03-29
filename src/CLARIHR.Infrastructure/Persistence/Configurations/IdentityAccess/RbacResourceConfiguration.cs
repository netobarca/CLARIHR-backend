using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class RbacResourceConfiguration : IEntityTypeConfiguration<RbacResource>
{
    public void Configure(EntityTypeBuilder<RbacResource> builder)
    {
        builder.ToTable("rbac_resource_catalog");

        builder.HasKey(resource => resource.Id)
            .HasName("pk_rbac_resource_catalog");

        builder.Property(resource => resource.Id)
            .HasColumnName("id");

        builder.Property(resource => resource.ResourceKey)
            .HasColumnName("resource_key")
            .HasMaxLength(100);

        builder.Property(resource => resource.NormalizedResourceKey)
            .HasColumnName("normalized_resource_key")
            .HasMaxLength(100);

        builder.Property(resource => resource.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(120);

        builder.Property(resource => resource.IsActive)
            .HasColumnName("is_active");

        builder.Property(resource => resource.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(resource => resource.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(resource => resource.NormalizedResourceKey)
            .IsUnique()
            .HasDatabaseName("uq_rbac_resource_catalog__normalized_resource_key");

        builder.HasIndex(resource => resource.ResourceKey)
            .IsUnique()
            .HasDatabaseName("uq_rbac_resource_catalog__resource_key");

        builder.HasData(GlobalCatalogSeedData.GetRbacResources());
    }
}
