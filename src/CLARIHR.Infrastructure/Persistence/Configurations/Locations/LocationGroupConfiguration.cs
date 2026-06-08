using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Locations;

internal sealed class LocationGroupConfiguration : IEntityTypeConfiguration<LocationGroup>
{
    public void Configure(EntityTypeBuilder<LocationGroup> builder)
    {
        builder.ToTable("location_groups");

        builder.HasKey(group => group.Id)
            .HasName("pk_location_groups");

        builder.Property(group => group.Id)
            .HasColumnName("id");

        builder.Property(group => group.PublicId)
            .HasColumnName("public_id");

        builder.Property(group => group.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(group => group.LevelOrder)
            .HasColumnName("level_order");

        builder.Property(group => group.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(group => group.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(group => group.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(group => group.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(group => group.ParentId)
            .HasColumnName("parent_id");

        builder.Property(group => group.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(group => group.IsActive)
            .HasColumnName("is_active");

        builder.Property(group => group.IsDefault)
            .HasColumnName("is_default");

        builder.Property(group => group.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(group => group.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(group => group.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(group => group.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_location_groups__public_id");

        builder.HasIndex(group => new { group.TenantId, group.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(LocationValidationRules.GroupCodeUniqueConstraintName);

        builder.HasIndex(group => new { group.TenantId, group.ParentId, group.NormalizedName })
            .HasDatabaseName("ix_location_groups__tenant_parent_name");

        builder.HasIndex(group => new { group.TenantId, group.LevelOrder, group.IsActive })
            .HasDatabaseName("ix_location_groups__tenant_level_active");

        builder.HasIndex(group => new { group.TenantId, group.ParentId, group.IsActive })
            .HasDatabaseName("ix_location_groups__tenant_parent_active");

        builder.HasIndex(group => new { group.TenantId, group.NormalizedName })
            .HasDatabaseName("ix_location_groups__tenant_name");

        builder.HasOne<LocationGroup>()
            .WithMany()
            .HasForeignKey(group => group.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_location_groups__parent");
    }
}
