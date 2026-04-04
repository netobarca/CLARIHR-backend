using CLARIHR.Domain.InternalCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.InternalCatalogs;

internal sealed class InternalCatalogValueConfiguration : IEntityTypeConfiguration<InternalCatalogValue>
{
    public void Configure(EntityTypeBuilder<InternalCatalogValue> builder)
    {
        builder.ToTable("internal_catalog_values");

        builder.HasKey(item => item.Id)
            .HasName("pk_internal_catalog_values");

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.PublicId)
            .HasColumnName("public_id");

        builder.Property(item => item.CatalogKey)
            .HasColumnName("catalog_key")
            .HasMaxLength(120);

        builder.Property(item => item.Value)
            .HasColumnName("value")
            .HasMaxLength(200);

        builder.Property(item => item.NormalizedValue)
            .HasColumnName("normalized_value")
            .HasMaxLength(200);

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property(item => item.CreatedByUserPublicId)
            .HasColumnName("created_by_user_public_id");

        builder.Property(item => item.UsageCount)
            .HasColumnName("usage_count");

        builder.Property(item => item.LastUsedAtUtc)
            .HasColumnName("last_used_at_utc");

        builder.Property(item => item.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(item => item.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_internal_catalog_values__public_id");

        builder.HasIndex(item => new { item.CatalogKey, item.NormalizedValue })
            .IsUnique()
            .HasDatabaseName("uq_internal_catalog_values__catalog_key_normalized_value");

        builder.HasIndex(item => new { item.CatalogKey, item.IsActive })
            .HasDatabaseName("ix_internal_catalog_values__catalog_key_active");
    }
}
