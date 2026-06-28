using CLARIHR.Domain.GeneralCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.GeneralCatalogs;

/// <summary>
/// EF configuration for the HR-dashboard age-range catalog. Reuses the country-scoped general-catalog base
/// (code/name/country/active/sort + HasData seed) and adds the two numeric bound columns the bucketization
/// needs (D-10). Seeded for SV.
/// </summary>
internal sealed class AgeRangeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<AgeRangeCatalogItem>
{
    public AgeRangeCatalogItemConfiguration()
        : base(
            "age_range_catalog_items",
            "pk_age_range_catalog_items",
            "uq_age_range_catalog_items__public_id",
            "uq_age_range_catalog_items__country_code",
            "ix_age_range_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetAgeRangeCatalogItems())
    {
    }

    public override void Configure(EntityTypeBuilder<AgeRangeCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.LowerBoundYears).HasColumnName("lower_bound_years");
        builder.Property(item => item.UpperBoundYears).HasColumnName("upper_bound_years");
    }
}

/// <summary>
/// EF configuration for the HR-dashboard seniority-range catalog. Bounds are stored in MONTHS. Seeded for SV.
/// </summary>
internal sealed class SeniorityRangeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<SeniorityRangeCatalogItem>
{
    public SeniorityRangeCatalogItemConfiguration()
        : base(
            "seniority_range_catalog_items",
            "pk_seniority_range_catalog_items",
            "uq_seniority_range_catalog_items__public_id",
            "uq_seniority_range_catalog_items__country_code",
            "ix_seniority_range_catalog_items__country_active_sort",
            GlobalCatalogSeedData.GetSeniorityRangeCatalogItems())
    {
    }

    public override void Configure(EntityTypeBuilder<SeniorityRangeCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.LowerBoundMonths).HasColumnName("lower_bound_months");
        builder.Property(item => item.UpperBoundMonths).HasColumnName("upper_bound_months");
    }
}
