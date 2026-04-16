using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal abstract class PersonnelReferenceCatalogItemConfigurationBase<TCatalogItem>(
    string tableName,
    string primaryKeyName,
    string publicIdIndexName,
    string countryCodeIndexName,
    string countryActiveSortIndexName)
    : IEntityTypeConfiguration<TCatalogItem>
    where TCatalogItem : PersonnelReferenceCatalogItemBase
{
    public virtual void Configure(EntityTypeBuilder<TCatalogItem> builder)
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

internal sealed class IdentificationTypeCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<IdentificationTypeCatalogItem>
{
    public IdentificationTypeCatalogItemConfiguration()
        : base(
            "identification_type_catalog_items",
            "pk_identification_type_catalog_items",
            "uq_identification_type_catalog_items__public_id",
            "uq_identification_type_catalog_items__country_code",
            "ix_identification_type_catalog_items__country_active_sort")
    {
    }
}

internal sealed class ProfessionCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<ProfessionCatalogItem>
{
    public ProfessionCatalogItemConfiguration()
        : base(
            "profession_catalog_items",
            "pk_profession_catalog_items",
            "uq_profession_catalog_items__public_id",
            "uq_profession_catalog_items__country_code",
            "ix_profession_catalog_items__country_active_sort")
    {
    }
}

internal sealed class MaritalStatusCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<MaritalStatusCatalogItem>
{
    public MaritalStatusCatalogItemConfiguration()
        : base(
            "marital_status_catalog_items",
            "pk_marital_status_catalog_items",
            "uq_marital_status_catalog_items__public_id",
            "uq_marital_status_catalog_items__country_code",
            "ix_marital_status_catalog_items__country_active_sort")
    {
    }
}

internal sealed class KinshipCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<KinshipCatalogItem>
{
    public KinshipCatalogItemConfiguration()
        : base(
            "kinship_catalog_items",
            "pk_kinship_catalog_items",
            "uq_kinship_catalog_items__public_id",
            "uq_kinship_catalog_items__country_code",
            "ix_kinship_catalog_items__country_active_sort")
    {
    }
}

internal sealed class DepartmentCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<DepartmentCatalogItem>
{
    public DepartmentCatalogItemConfiguration()
        : base(
            "department_catalog_items",
            "pk_department_catalog_items",
            "uq_department_catalog_items__public_id",
            "uq_department_catalog_items__country_code",
            "ix_department_catalog_items__country_active_sort")
    {
    }
}

internal sealed class MunicipalityCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<MunicipalityCatalogItem>
{
    public MunicipalityCatalogItemConfiguration()
        : base(
            "municipality_catalog_items",
            "pk_municipality_catalog_items",
            "uq_municipality_catalog_items__public_id",
            "uq_municipality_catalog_items__country_code",
            "ix_municipality_catalog_items__country_active_sort")
    {
    }

    public override void Configure(EntityTypeBuilder<MunicipalityCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.DepartmentCatalogItemId)
            .HasColumnName("department_catalog_item_id");

        builder.HasOne(item => item.DepartmentCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.DepartmentCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_municipality_catalog_items__department");

        builder.HasIndex(item => new { item.DepartmentCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_municipality_catalog_items__department_active_sort");
    }
}
