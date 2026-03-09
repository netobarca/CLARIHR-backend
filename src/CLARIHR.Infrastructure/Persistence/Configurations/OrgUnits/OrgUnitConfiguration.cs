using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.OrgStructureCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.OrgUnits;

internal sealed class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.ToTable("org_units");

        builder.HasKey(orgUnit => orgUnit.Id)
            .HasName("pk_org_units");

        builder.Property(orgUnit => orgUnit.Id)
            .HasColumnName("id");

        builder.Property(orgUnit => orgUnit.PublicId)
            .HasColumnName("public_id");

        builder.Property(orgUnit => orgUnit.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(orgUnit => orgUnit.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(orgUnit => orgUnit.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(50);

        builder.Property(orgUnit => orgUnit.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(orgUnit => orgUnit.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(orgUnit => orgUnit.OrgUnitTypeCatalogItemId)
            .HasColumnName("org_unit_type_catalog_item_id");

        builder.Property(orgUnit => orgUnit.FunctionalAreaCatalogItemId)
            .HasColumnName("functional_area_catalog_item_id");

        builder.Property(orgUnit => orgUnit.ParentId)
            .HasColumnName("parent_id");

        builder.Property(orgUnit => orgUnit.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(orgUnit => orgUnit.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(orgUnit => orgUnit.CostCenterCode)
            .HasColumnName("cost_center_code")
            .HasMaxLength(100);

        builder.Property(orgUnit => orgUnit.ManagerEmployeeId)
            .HasColumnName("manager_employee_id");

        builder.Property(orgUnit => orgUnit.IsActive)
            .HasColumnName("is_active");

        builder.Property(orgUnit => orgUnit.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(orgUnit => orgUnit.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(orgUnit => orgUnit.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(orgUnit => orgUnit.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_org_units__public_id");

        builder.HasIndex(orgUnit => new { orgUnit.TenantId, orgUnit.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_org_units__tenant_code");

        builder.HasIndex(orgUnit => new { orgUnit.TenantId, orgUnit.ParentId })
            .HasDatabaseName("ix_org_units__tenant_parent");

        builder.HasIndex(orgUnit => new { orgUnit.TenantId, orgUnit.IsActive })
            .HasDatabaseName("ix_org_units__tenant_active");

        builder.HasIndex(orgUnit => new { orgUnit.TenantId, orgUnit.NormalizedName })
            .HasDatabaseName("ix_org_units__tenant_name");

        builder.HasIndex(orgUnit => new { orgUnit.TenantId, orgUnit.OrgUnitTypeCatalogItemId })
            .HasDatabaseName("ix_org_units__tenant_org_unit_type_catalog_item");

        builder.HasIndex(orgUnit => new { orgUnit.TenantId, orgUnit.FunctionalAreaCatalogItemId })
            .HasDatabaseName("ix_org_units__tenant_functional_area_catalog_item");

        builder.HasOne<OrgUnit>()
            .WithMany()
            .HasForeignKey(orgUnit => orgUnit.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_org_units__parent");

        builder.HasOne<OrgUnitTypeCatalogItem>()
            .WithMany()
            .HasForeignKey(orgUnit => orgUnit.OrgUnitTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_org_units__org_unit_type_catalog_item");

        builder.HasOne<FunctionalAreaCatalogItem>()
            .WithMany()
            .HasForeignKey(orgUnit => orgUnit.FunctionalAreaCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_org_units__functional_area_catalog_item");
    }
}
