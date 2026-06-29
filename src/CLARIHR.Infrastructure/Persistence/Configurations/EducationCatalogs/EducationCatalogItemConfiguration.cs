using CLARIHR.Domain.EducationCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.EducationCatalogs;

/// <summary>
/// Base EF configuration for system-scoped education catalog items.
/// No country columns — these are global system catalogs.
/// </summary>
internal abstract class EducationCatalogItemConfigurationBase<TCatalogItem>(
    string tableName,
    string primaryKeyName,
    string publicIdIndexName,
    string uniqueCodeIndexName,
    string activeSortIndexName,
    IEnumerable<object>? seedData = null)
    : IEntityTypeConfiguration<TCatalogItem>
    where TCatalogItem : EducationCatalogItem
{
    public void Configure(EntityTypeBuilder<TCatalogItem> builder)
    {
        builder.ToTable(tableName);

        builder.HasKey(item => item.Id)
            .HasName(primaryKeyName);

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName(publicIdIndexName);

        builder.HasIndex(item => item.NormalizedCode)
            .IsUnique()
            .HasDatabaseName(uniqueCodeIndexName);

        builder.HasIndex(item => new { item.IsActive, item.SortOrder })
            .HasDatabaseName(activeSortIndexName);

        // System-scoped education catalogs were previously seeded only by DevSeedService; opt in to static HasData
        // so they reach every environment via the migration pipeline (codes stay stable — records reference them).
        if (seedData is not null)
        {
            builder.HasData(seedData);
        }
    }
}

internal sealed class EducationStatusCatalogItemConfiguration
    : EducationCatalogItemConfigurationBase<EducationStatusCatalogItem>
{
    public EducationStatusCatalogItemConfiguration()
        : base(
            "education_status_catalog_items",
            "pk_education_status_catalog_items",
            "uq_education_status_catalog_items__public_id",
            "uq_education_status_catalog_items__code",
            "ix_education_status_catalog_items__active_sort",
            GlobalCatalogSeedData.GetEducationStatusCatalogItems())
    {
    }
}

internal sealed class EducationStudyTypeCatalogItemConfiguration
    : EducationCatalogItemConfigurationBase<EducationStudyTypeCatalogItem>
{
    public EducationStudyTypeCatalogItemConfiguration()
        : base(
            "education_study_type_catalog_items",
            "pk_education_study_type_catalog_items",
            "uq_education_study_type_catalog_items__public_id",
            "uq_education_study_type_catalog_items__code",
            "ix_education_study_type_catalog_items__active_sort",
            GlobalCatalogSeedData.GetEducationStudyTypeCatalogItems())
    {
    }
}

internal sealed class EducationCareerCatalogItemConfiguration
    : EducationCatalogItemConfigurationBase<EducationCareerCatalogItem>
{
    public EducationCareerCatalogItemConfiguration()
        : base(
            "education_career_catalog_items",
            "pk_education_career_catalog_items",
            "uq_education_career_catalog_items__public_id",
            "uq_education_career_catalog_items__code",
            "ix_education_career_catalog_items__active_sort",
            GlobalCatalogSeedData.GetEducationCareerCatalogItems())
    {
    }
}

internal sealed class EducationShiftCatalogItemConfiguration
    : EducationCatalogItemConfigurationBase<EducationShiftCatalogItem>
{
    public EducationShiftCatalogItemConfiguration()
        : base(
            "education_shift_catalog_items",
            "pk_education_shift_catalog_items",
            "uq_education_shift_catalog_items__public_id",
            "uq_education_shift_catalog_items__code",
            "ix_education_shift_catalog_items__active_sort",
            GlobalCatalogSeedData.GetEducationShiftCatalogItems())
    {
    }
}

internal sealed class EducationModalityCatalogItemConfiguration
    : EducationCatalogItemConfigurationBase<EducationModalityCatalogItem>
{
    public EducationModalityCatalogItemConfiguration()
        : base(
            "education_modality_catalog_items",
            "pk_education_modality_catalog_items",
            "uq_education_modality_catalog_items__public_id",
            "uq_education_modality_catalog_items__code",
            "ix_education_modality_catalog_items__active_sort",
            GlobalCatalogSeedData.GetEducationModalityCatalogItems())
    {
    }
}
