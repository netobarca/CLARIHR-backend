using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class RetirementCategoryCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<RetirementCategoryCatalogItem>
{
    public RetirementCategoryCatalogItemConfiguration()
        : base(
            "retirement_category_catalog_items",
            "pk_retirement_category_catalog_items",
            "uq_retirement_category_catalog_items__public_id",
            "uq_retirement_category_catalog_items__country_code",
            "ix_retirement_category_catalog_items__country_active_sort")
    {
    }

    public override void Configure(EntityTypeBuilder<RetirementCategoryCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.SeparationType)
            .HasColumnName("separation_type")
            .HasMaxLength(20)
            .HasConversion<string>();

        // Seeded in EVERY environment via the migration pipeline (D-13) so the baja flow always has
        // active categories to validate against, not only fresh dev databases.
        builder.HasData(GlobalCatalogSeedData.GetRetirementCategoryCatalogItems());
    }
}

internal sealed class RetirementReasonCatalogItemConfiguration
    : PersonnelReferenceCatalogItemConfigurationBase<RetirementReasonCatalogItem>
{
    public RetirementReasonCatalogItemConfiguration()
        : base(
            "retirement_reason_catalog_items",
            "pk_retirement_reason_catalog_items",
            "uq_retirement_reason_catalog_items__public_id",
            "uq_retirement_reason_catalog_items__country_code",
            "ix_retirement_reason_catalog_items__country_active_sort")
    {
    }

    public override void Configure(EntityTypeBuilder<RetirementReasonCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.RetirementCategoryCatalogItemId)
            .HasColumnName("retirement_category_catalog_item_id");

        builder.HasOne(item => item.RetirementCategoryCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.RetirementCategoryCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_retirement_reason_catalog_items__category");

        builder.HasIndex(item => new { item.RetirementCategoryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_retirement_reason_catalog_items__category_active_sort");

        // Seeded in EVERY environment via the migration pipeline (D-13).
        builder.HasData(GlobalCatalogSeedData.GetRetirementReasonCatalogItems());
    }

    // A reason code can repeat under different categories (e.g. MUTUO_ACUERDO / FALLECIMIENTO as both a
    // category and its single reason), so the unique key must include the category — mirrors
    // InsuranceRange under InsuranceType.
    protected override void ConfigureUniqueCodeIndex(EntityTypeBuilder<RetirementReasonCatalogItem> builder) =>
        builder.HasIndex(item => new { item.CountryCatalogItemId, item.RetirementCategoryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_retirement_reason_catalog_items__country_code");
}
