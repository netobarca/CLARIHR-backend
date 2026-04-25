using CLARIHR.Domain.Banks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Banks;

internal sealed class BankCatalogItemConfiguration : IEntityTypeConfiguration<BankCatalogItem>
{
    public void Configure(EntityTypeBuilder<BankCatalogItem> builder)
    {
        builder.ToTable("bank_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_bank_catalog_items");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CountryCatalogItemId).HasColumnName("country_catalog_item_id");
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(item => item.Alias).HasColumnName("alias").HasMaxLength(120);
        builder.Property(item => item.NormalizedAlias).HasColumnName("normalized_alias").HasMaxLength(120);
        builder.Property(item => item.SwiftCode).HasColumnName("swift_code").HasMaxLength(40);
        builder.Property(item => item.NormalizedSwiftCode).HasColumnName("normalized_swift_code").HasMaxLength(40);
        builder.Property(item => item.RoutingCode).HasColumnName("routing_code").HasMaxLength(40);
        builder.Property(item => item.NormalizedRoutingCode).HasColumnName("normalized_routing_code").HasMaxLength(40);
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
            .HasDatabaseName("uq_bank_catalog_items__public_id");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_bank_catalog_items__country_code");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_bank_catalog_items__country_active_sort");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedName })
            .HasDatabaseName("ix_bank_catalog_items__country_name");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedAlias })
            .HasDatabaseName("ix_bank_catalog_items__country_alias");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedSwiftCode })
            .HasDatabaseName("ix_bank_catalog_items__country_swift");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedRoutingCode })
            .HasDatabaseName("ix_bank_catalog_items__country_routing");

        builder.HasData(GlobalCatalogSeedData.GetBankCatalogItems());
    }
}
