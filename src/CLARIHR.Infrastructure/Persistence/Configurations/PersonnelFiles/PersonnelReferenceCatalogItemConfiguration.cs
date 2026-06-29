using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal abstract class PersonnelReferenceCatalogItemConfigurationBase<TCatalogItem>(
    string tableName,
    string primaryKeyName,
    string publicIdIndexName,
    string countryCodeIndexName,
    string countryActiveSortIndexName,
    IEnumerable<object>? seedData = null)
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

        ConfigureUniqueCodeIndex(builder);

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName(countryActiveSortIndexName);

        // Most reference catalogs are seeded per-country at runtime (DevSeedService); the ones that must exist in
        // every environment opt in by passing static HasData here (insurance types/ranges — same proven pattern as
        // the general catalogs). The parent type must be seeded for the range FK to resolve in the same migration.
        if (seedData is not null)
        {
            builder.HasData(seedData);
        }
    }

    /// <summary>
    /// Declares the per-country uniqueness of the catalog code. Most reference catalogs are unique on
    /// <c>(country, normalized code)</c>; a subclass whose code is scoped to a parent (e.g. an insurance
    /// range under an insurance type) overrides this to widen the key so the same code may legitimately
    /// repeat under different parents.
    /// </summary>
    protected virtual void ConfigureUniqueCodeIndex(EntityTypeBuilder<TCatalogItem> builder) =>
        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(countryCodeIndexName);
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

internal sealed class InsuranceTypeCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<InsuranceTypeCatalogItem>
{
    public InsuranceTypeCatalogItemConfiguration()
        : base(
            "insurance_type_catalog_items",
            "pk_insurance_type_catalog_items",
            "uq_insurance_type_catalog_items__public_id",
            "uq_insurance_type_catalog_items__country_code",
            "ix_insurance_type_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetInsuranceTypeCatalogItems())
    {
    }
}

internal sealed class InsuranceRangeCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<InsuranceRangeCatalogItem>
{
    public InsuranceRangeCatalogItemConfiguration()
        : base(
            "insurance_range_catalog_items",
            "pk_insurance_range_catalog_items",
            "uq_insurance_range_catalog_items__public_id",
            "uq_insurance_range_catalog_items__country_code",
            "ix_insurance_range_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetInsuranceRangeCatalogItems())
    {
    }

    public override void Configure(EntityTypeBuilder<InsuranceRangeCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.InsuranceTypeCatalogItemId)
            .HasColumnName("insurance_type_catalog_item_id");

        builder.HasOne(item => item.InsuranceTypeCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.InsuranceTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_insurance_range_catalog_items__insurance_type");

        builder.HasIndex(item => new { item.InsuranceTypeCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_insurance_range_catalog_items__type_active_sort");
    }

    // A range code (BASICO/INTERMEDIO/PREMIUM/…) is scoped to its insurance type, so the same code may
    // legitimately repeat under different types — the unique key must include the type, not just
    // (country, code). Keeping the base (country, code) key would reject the second "BASICO".
    protected override void ConfigureUniqueCodeIndex(EntityTypeBuilder<InsuranceRangeCatalogItem> builder) =>
        builder.HasIndex(item => new { item.CountryCatalogItemId, item.InsuranceTypeCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_insurance_range_catalog_items__country_code");
}
