using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class SettlementConceptCatalogItemConfiguration : IEntityTypeConfiguration<SettlementConceptCatalogItem>
{
    public void Configure(EntityTypeBuilder<SettlementConceptCatalogItem> builder)
    {
        builder.ToTable("settlement_concept_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_settlement_concept_catalog_items");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CountryCatalogItemId).HasColumnName("country_catalog_item_id");
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(item => item.ConceptClass).HasColumnName("concept_class").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.AffectsIsss).HasColumnName("affects_isss");
        builder.Property(item => item.AffectsAfp).HasColumnName("affects_afp");
        builder.Property(item => item.AffectsRenta).HasColumnName("affects_renta");
        builder.Property(item => item.ExemptionRule).HasColumnName("exemption_rule").HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.ExemptionMultiplier).HasColumnName("exemption_multiplier").HasColumnType("numeric(11,8)");
        builder.Property(item => item.IsSystemCalculated).HasColumnName("is_system_calculated");
        builder.Property(item => item.DefaultRatePercent).HasColumnName("default_rate_percent").HasColumnType("numeric(11,8)");
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
            .HasDatabaseName("uq_settlement_concept_catalog_items__public_id");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_settlement_concept_catalog_items__country_code");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_settlement_concept_catalog_items__country_active_sort");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.ConceptClass, item.IsActive })
            .HasDatabaseName("ix_settlement_concept_catalog_items__country_class_active");

        // Seeded in EVERY environment via the migration pipeline (settlement module RF-015): the engine
        // suggests and validates lines against these 17 SV concepts, so they must exist beyond dev.
        builder.HasData(GlobalCatalogSeedData.GetSettlementConceptCatalogItems());
    }
}
