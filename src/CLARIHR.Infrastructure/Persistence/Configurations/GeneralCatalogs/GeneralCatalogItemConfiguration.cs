using CLARIHR.Domain.GeneralCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.GeneralCatalogs;

internal abstract class GeneralCatalogItemConfigurationBase<TCatalogItem>(
    string tableName,
    string primaryKeyName,
    string publicIdIndexName,
    string countryCodeIndexName,
    string countryActiveSortIndexName)
    : IEntityTypeConfiguration<TCatalogItem>
    where TCatalogItem : GeneralCatalogItem
{
    public void Configure(EntityTypeBuilder<TCatalogItem> builder)
    {
        builder.ToTable(tableName);

        builder.HasKey(item => item.Id)
            .HasName(primaryKeyName);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CountryCatalogItemId).HasColumnName("country_catalog_item_id");
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
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
            .HasDatabaseName(publicIdIndexName);

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(countryCodeIndexName);

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName(countryActiveSortIndexName);
    }
}

internal sealed class LanguageCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<LanguageCatalogItem>
{
    public LanguageCatalogItemConfiguration()
        : base(
            "language_catalog_items",
            "pk_language_catalog_items",
            "uq_language_catalog_items__public_id",
            "uq_language_catalog_items__country_code",
            "ix_language_catalog_items__country_active_sort")
    {
    }
}

internal sealed class LanguageLevelCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<LanguageLevelCatalogItem>
{
    public LanguageLevelCatalogItemConfiguration()
        : base(
            "language_level_catalog_items",
            "pk_language_level_catalog_items",
            "uq_language_level_catalog_items__public_id",
            "uq_language_level_catalog_items__country_code",
            "ix_language_level_catalog_items__country_active_sort")
    {
    }
}

internal sealed class TrainingTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<TrainingTypeCatalogItem>
{
    public TrainingTypeCatalogItemConfiguration()
        : base(
            "training_type_catalog_items",
            "pk_training_type_catalog_items",
            "uq_training_type_catalog_items__public_id",
            "uq_training_type_catalog_items__country_code",
            "ix_training_type_catalog_items__country_active_sort")
    {
    }
}

internal sealed class DurationUnitCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<DurationUnitCatalogItem>
{
    public DurationUnitCatalogItemConfiguration()
        : base(
            "duration_unit_catalog_items",
            "pk_duration_unit_catalog_items",
            "uq_duration_unit_catalog_items__public_id",
            "uq_duration_unit_catalog_items__country_code",
            "ix_duration_unit_catalog_items__country_active_sort")
    {
    }
}

internal sealed class ReferenceTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<ReferenceTypeCatalogItem>
{
    public ReferenceTypeCatalogItemConfiguration()
        : base(
            "reference_type_catalog_items",
            "pk_reference_type_catalog_items",
            "uq_reference_type_catalog_items__public_id",
            "uq_reference_type_catalog_items__country_code",
            "ix_reference_type_catalog_items__country_active_sort")
    {
    }
}

internal sealed class CurrencyCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<CurrencyCatalogItem>
{
    public CurrencyCatalogItemConfiguration()
        : base(
            "currency_catalog_items",
            "pk_currency_catalog_items",
            "uq_currency_catalog_items__public_id",
            "uq_currency_catalog_items__country_code",
            "ix_currency_catalog_items__country_active_sort")
    {
    }
}
