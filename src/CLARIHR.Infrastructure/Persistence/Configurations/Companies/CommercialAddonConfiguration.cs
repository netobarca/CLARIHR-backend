using CLARIHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class CommercialAddonConfiguration : IEntityTypeConfiguration<CommercialAddon>
{
    public void Configure(EntityTypeBuilder<CommercialAddon> builder)
    {
        builder.ToTable("commercial_addons");

        builder.HasKey(addon => addon.Id)
            .HasName("pk_commercial_addons");

        builder.Property(addon => addon.Id)
            .HasColumnName("id");

        builder.Property(addon => addon.PublicId)
            .HasColumnName("public_id");

        builder.Property(addon => addon.Code)
            .HasColumnName("code")
            .HasMaxLength(40);

        builder.Property(addon => addon.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(40);

        builder.Property(addon => addon.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(addon => addon.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(addon => addon.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(addon => addon.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(addon => addon.BillingModel)
            .HasColumnName("billing_model")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(addon => addon.MeasurementUnit)
            .HasColumnName("measurement_unit")
            .HasMaxLength(80);

        builder.Property(addon => addon.UnitPrice)
            .HasColumnName("unit_price")
            .HasPrecision(18, 2);

        builder.Property(addon => addon.MinimumQuantity)
            .HasColumnName("minimum_quantity");

        builder.Property(addon => addon.MinimumMonthlyFee)
            .HasColumnName("minimum_monthly_fee")
            .HasPrecision(18, 2);

        builder.Property(addon => addon.Periodicity)
            .HasColumnName("periodicity")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(addon => addon.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(addon => addon.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(addon => addon.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(addon => addon.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(addon => addon.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_commercial_addons__public_id");

        builder.HasIndex(addon => addon.NormalizedCode)
            .IsUnique()
            .HasDatabaseName("uq_commercial_addons__normalized_code");

        builder.HasIndex(addon => addon.NormalizedName)
            .HasDatabaseName("ix_commercial_addons__normalized_name");

        builder.HasIndex(addon => addon.Status)
            .HasDatabaseName("ix_commercial_addons__status");

        builder.HasIndex(addon => addon.Type)
            .HasDatabaseName("ix_commercial_addons__type");

        builder.HasIndex(addon => addon.BillingModel)
            .HasDatabaseName("ix_commercial_addons__billing_model");

        builder.HasMany(addon => addon.Entitlements)
            .WithOne()
            .HasForeignKey(entitlement => entitlement.CommercialAddonId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_commercial_addon_entitlements__commercial_addons");

        builder.Navigation(addon => addon.Entitlements).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class CommercialAddonEntitlementConfiguration : IEntityTypeConfiguration<CommercialAddonEntitlement>
{
    public void Configure(EntityTypeBuilder<CommercialAddonEntitlement> builder)
    {
        builder.ToTable("commercial_addon_entitlements");

        builder.HasKey(entitlement => entitlement.Id)
            .HasName("pk_commercial_addon_entitlements");

        builder.Property(entitlement => entitlement.Id)
            .HasColumnName("id");

        builder.Property(entitlement => entitlement.CommercialAddonId)
            .HasColumnName("commercial_addon_id");

        builder.Property(entitlement => entitlement.AddonCode)
            .HasColumnName("addon_code")
            .HasMaxLength(40);

        builder.Property(entitlement => entitlement.CapabilityCode)
            .HasColumnName("capability_code")
            .HasMaxLength(80);

        builder.Property(entitlement => entitlement.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(60);

        builder.Property(entitlement => entitlement.IsEnabled)
            .HasColumnName("is_enabled");

        builder.Property(entitlement => entitlement.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(entitlement => entitlement.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(entitlement => new { entitlement.CommercialAddonId, entitlement.ModuleKey })
            .IsUnique()
            .HasDatabaseName("uq_commercial_addon_entitlements__addon_module");

        builder.HasIndex(entitlement => new { entitlement.CommercialAddonId, entitlement.CapabilityCode })
            .IsUnique()
            .HasDatabaseName("uq_commercial_addon_entitlements__addon_capability");
    }
}
