using CLARIHR.Domain.GeneralCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.GeneralCatalogs;

internal sealed class FormControlTypeCatalogItemConfiguration
    : GeneralCatalogItemConfigurationBase<FormControlTypeCatalogItem>
{
    public FormControlTypeCatalogItemConfiguration()
        : base(
            "form_control_type_catalog_items",
            "pk_form_control_type_catalog_items",
            "uq_form_control_type_catalog_items__public_id",
            "uq_form_control_type_catalog_items__country_code",
            "ix_form_control_type_catalog_items__country_active_sort")
    {
    }

    public override void Configure(EntityTypeBuilder<FormControlTypeCatalogItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.ValueKind)
            .HasColumnName("value_kind")
            .HasMaxLength(20)
            .HasConversion<string>();

        builder.Property(item => item.SupportsOptions).HasColumnName("supports_options");
        builder.Property(item => item.SupportsRange).HasColumnName("supports_range");
        builder.Property(item => item.SupportsMultiple).HasColumnName("supports_multiple");

        // Closed system catalog — seeded in EVERY environment via the migration pipeline (D-08).
        builder.HasData(GlobalCatalogSeedData.GetFormControlTypeCatalogItems());
    }
}
