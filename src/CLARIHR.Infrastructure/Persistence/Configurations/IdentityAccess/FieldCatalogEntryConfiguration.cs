using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class FieldCatalogEntryConfiguration : IEntityTypeConfiguration<FieldCatalogEntry>
{
    public void Configure(EntityTypeBuilder<FieldCatalogEntry> builder)
    {
        builder.ToTable("field_catalog");

        builder.HasKey(entry => entry.Id)
            .HasName("pk_field_catalog");

        builder.Property(entry => entry.Id)
            .HasColumnName("id");

        builder.Property(entry => entry.FieldKey)
            .HasColumnName("field_key")
            .HasMaxLength(150);

        builder.Property(entry => entry.NormalizedFieldKey)
            .HasColumnName("normalized_field_key")
            .HasMaxLength(150);

        builder.Property(entry => entry.ResourceKey)
            .HasColumnName("resource_key")
            .HasMaxLength(100);

        builder.Property(entry => entry.NormalizedResourceKey)
            .HasColumnName("normalized_resource_key")
            .HasMaxLength(100);

        builder.Property(entry => entry.PropertyName)
            .HasColumnName("property_name")
            .HasMaxLength(100);

        builder.Property(entry => entry.NormalizedPropertyName)
            .HasColumnName("normalized_property_name")
            .HasMaxLength(100);

        builder.Property(entry => entry.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(120);

        builder.Property(entry => entry.IsConfigurable)
            .HasColumnName("is_configurable");

        builder.Property(entry => entry.IsSensitive)
            .HasColumnName("is_sensitive");

        builder.Property(entry => entry.DataType)
            .HasColumnName("data_type")
            .HasMaxLength(40);

        builder.Property(entry => entry.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(entry => entry.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(entry => entry.NormalizedFieldKey)
            .IsUnique()
            .HasDatabaseName("uq_field_catalog__normalized_field_key");

        builder.HasIndex(entry => new { entry.NormalizedResourceKey, entry.IsConfigurable })
            .HasDatabaseName("ix_field_catalog__resource_configurable");

        builder.HasData(GlobalCatalogSeedData.GetFieldCatalogEntries());
    }
}
