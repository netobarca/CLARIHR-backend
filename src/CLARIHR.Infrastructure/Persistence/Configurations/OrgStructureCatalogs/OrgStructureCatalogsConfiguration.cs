using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.OrgStructureCatalogs;

internal sealed class CompanyTypeCatalogItemConfiguration : IEntityTypeConfiguration<CompanyTypeCatalogItem>
{
    public void Configure(EntityTypeBuilder<CompanyTypeCatalogItem> builder)
    {
        builder.ToTable("company_type_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_company_type_catalog_items");

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.PublicId)
            .HasColumnName("public_id");

        builder.Property(item => item.CountryCatalogItemId)
            .HasColumnName("country_catalog_item_id");

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(item => item.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(item => item.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(item => item.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(item => item.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasOne<CountryCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.CountryCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_company_type_catalog_items__public_id");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_company_type_catalog_items__country_code");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedName })
            .HasDatabaseName("ix_company_type_catalog_items__country_name");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive })
            .HasDatabaseName("ix_company_type_catalog_items__country_active");

        builder.HasData(CompanyTypeCatalog.Items.Select(static item => new
        {
            Id = item.Id,
            PublicId = GlobalCatalogSeedData.CreateSeedPublicId("COMPANY_TYPE", $"{item.CountryCode}:{item.Code}"),
            CountryCatalogItemId = item.CountryCatalogItemId,
            Code = item.Code,
            NormalizedCode = item.Code.ToUpperInvariant(),
            Name = item.Name,
            NormalizedName = item.Name.Trim().ToUpperInvariant(),
            Description = item.Description,
            SortOrder = item.SortOrder,
            IsActive = true,
            ConcurrencyToken = GlobalCatalogSeedData.CreateSeedPublicId("COMPANY_TYPE_TOKEN", $"{item.CountryCode}:{item.Code}"),
            CreatedUtc = GlobalCatalogSeedData.SeededAtUtc,
            ModifiedUtc = (DateTime?)GlobalCatalogSeedData.SeededAtUtc
        }));
    }
}

internal sealed class OrgUnitTypeCatalogItemConfiguration : IEntityTypeConfiguration<OrgUnitTypeCatalogItem>
{
    public void Configure(EntityTypeBuilder<OrgUnitTypeCatalogItem> builder)
    {
        builder.ToTable("org_unit_type_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_org_unit_type_catalog_items");

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.PublicId)
            .HasColumnName("public_id");

        builder.Property(item => item.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(item => item.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(item => item.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(item => item.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(item => item.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_org_unit_type_catalog_items__public_id");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(OrgStructureCatalogValidationRules.UnitTypeCodeUniqueConstraintName);

        builder.HasIndex(item => new { item.TenantId, item.NormalizedName })
            .HasDatabaseName("ix_org_unit_type_catalog_items__tenant_name");

        builder.HasIndex(item => new { item.TenantId, item.IsActive })
            .HasDatabaseName("ix_org_unit_type_catalog_items__tenant_active");
    }
}

internal sealed class FunctionalAreaCatalogItemConfiguration : IEntityTypeConfiguration<FunctionalAreaCatalogItem>
{
    public void Configure(EntityTypeBuilder<FunctionalAreaCatalogItem> builder)
    {
        builder.ToTable("functional_area_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_functional_area_catalog_items");

        builder.Property(item => item.Id)
            .HasColumnName("id");

        builder.Property(item => item.PublicId)
            .HasColumnName("public_id");

        builder.Property(item => item.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(item => item.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(item => item.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(item => item.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property(item => item.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(item => item.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(item => item.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_functional_area_catalog_items__public_id");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(OrgStructureCatalogValidationRules.FunctionalAreaCodeUniqueConstraintName);

        builder.HasIndex(item => new { item.TenantId, item.NormalizedName })
            .HasDatabaseName("ix_functional_area_catalog_items__tenant_name");

        builder.HasIndex(item => new { item.TenantId, item.IsActive })
            .HasDatabaseName("ix_functional_area_catalog_items__tenant_active");
    }
}
