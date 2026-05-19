using CLARIHR.Domain.CatalogTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.CatalogTypes;

internal sealed class CatalogTypeDescriptorConfiguration
    : IEntityTypeConfiguration<CatalogTypeDescriptor>
{
    public void Configure(EntityTypeBuilder<CatalogTypeDescriptor> builder)
    {
        builder.ToTable("catalog_type_descriptors");

        builder.HasKey(item => item.Id)
            .HasName("pk_catalog_type_descriptors");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_catalog_type_descriptors__public_id");

        builder.HasIndex(item => item.NormalizedCode)
            .IsUnique()
            .HasDatabaseName("uq_catalog_type_descriptors__code");

        builder.HasIndex(item => new { item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_catalog_type_descriptors__active_sort");
    }
}
