using CLARIHR.Domain.PositionDescriptionCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PositionDescriptionCatalogs;

internal sealed class PositionDescriptionCatalogItemConfiguration : IEntityTypeConfiguration<PositionDescriptionCatalogItem>
{
    public void Configure(EntityTypeBuilder<PositionDescriptionCatalogItem> builder)
    {
        builder.ToTable("position_description_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_position_description_catalog_items");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");

        builder.Property(item => item.CatalogType)
            .HasColumnName("catalog_type")
            .HasConversion<string>()
            .HasMaxLength(50);

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

        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_position_description_catalog_items__public_id");

        builder.HasIndex(item => new { item.TenantId, item.CatalogType, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_position_description_catalog_items__tenant_type_code");

        builder.HasIndex(item => new { item.TenantId, item.CatalogType, item.NormalizedName })
            .HasDatabaseName("ix_position_description_catalog_items__tenant_type_name");

        builder.HasIndex(item => new { item.TenantId, item.CatalogType, item.IsActive })
            .HasDatabaseName("ix_position_description_catalog_items__tenant_type_active");
    }
}

internal sealed class PositionCategoryClassificationConfiguration : IEntityTypeConfiguration<PositionCategoryClassification>
{
    public void Configure(EntityTypeBuilder<PositionCategoryClassification> builder)
    {
        builder.ToTable("position_category_classifications");

        builder.HasKey(item => item.Id)
            .HasName("pk_position_category_classifications");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");

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

        builder.Property(item => item.PositionFunctionCatalogItemId).HasColumnName("position_function_catalog_item_id");
        builder.Property(item => item.PositionContractCatalogItemId).HasColumnName("position_contract_catalog_item_id");
        builder.Property(item => item.OrgUnitTypeCatalogItemId).HasColumnName("org_unit_type_catalog_item_id");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_position_category_classifications__public_id");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_position_category_classifications__tenant_code");

        builder.HasIndex(item => new
            {
                item.TenantId,
                item.PositionFunctionCatalogItemId,
                item.PositionContractCatalogItemId,
                item.OrgUnitTypeCatalogItemId
            })
            .IsUnique()
            .HasDatabaseName("uq_position_category_classifications__tenant_axes");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedName })
            .HasDatabaseName("ix_position_category_classifications__tenant_name");

        builder.HasIndex(item => new { item.TenantId, item.IsActive })
            .HasDatabaseName("ix_position_category_classifications__tenant_active");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.PositionFunctionCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_category_classifications__position_function_catalog_item");

        builder.HasOne<PositionDescriptionCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.PositionContractCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_category_classifications__position_contract_catalog_item");

        builder.HasOne<CLARIHR.Domain.OrgStructureCatalogs.OrgUnitTypeCatalogItem>()
            .WithMany()
            .HasForeignKey(item => item.OrgUnitTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_category_classifications__org_unit_type_catalog_item");
    }
}

internal sealed class PositionCategoryConfiguration : IEntityTypeConfiguration<PositionCategory>
{
    public void Configure(EntityTypeBuilder<PositionCategory> builder)
    {
        builder.ToTable("position_categories");

        builder.HasKey(item => item.Id)
            .HasName("pk_position_categories");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");

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

        builder.Property(item => item.PositionCategoryClassificationId).HasColumnName("position_category_classification_id");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_position_categories__public_id");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_position_categories__tenant_code");

        builder.HasIndex(item => new { item.TenantId, item.PositionCategoryClassificationId })
            .HasDatabaseName("ix_position_categories__tenant_classification");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedName })
            .HasDatabaseName("ix_position_categories__tenant_name");

        builder.HasIndex(item => new { item.TenantId, item.IsActive })
            .HasDatabaseName("ix_position_categories__tenant_active");

        builder.HasOne<PositionCategoryClassification>()
            .WithMany()
            .HasForeignKey(item => item.PositionCategoryClassificationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_position_categories__position_category_classification");
    }
}
