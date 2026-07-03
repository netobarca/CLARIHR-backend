using CLARIHR.Domain.Afps;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Afps;

internal sealed class AfpCatalogItemConfiguration : IEntityTypeConfiguration<AfpCatalogItem>
{
    public void Configure(EntityTypeBuilder<AfpCatalogItem> builder)
    {
        builder.ToTable("afp_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_afp_catalog_items");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CountryCatalogItemId).HasColumnName("country_catalog_item_id");
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(item => item.Abbreviation).HasColumnName("abbreviation").HasMaxLength(20);
        builder.Property(item => item.Address).HasColumnName("address").HasMaxLength(500);
        builder.Property(item => item.Phone).HasColumnName("phone").HasMaxLength(40);
        builder.Property(item => item.Fax).HasColumnName("fax").HasMaxLength(40);
        builder.Property(item => item.ContactName).HasColumnName("contact_name").HasMaxLength(150);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.CountryCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.CountryCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_afp_catalog_items__public_id");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_afp_catalog_items__country_code");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_afp_catalog_items__country_active_sort");

        builder.HasData(GlobalCatalogSeedData.GetAfpCatalogItems());
    }
}
