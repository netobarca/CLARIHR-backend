using CLARIHR.Domain.Compensation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Compensation;

internal sealed class CompensationConceptTypeCatalogItemConfiguration : IEntityTypeConfiguration<CompensationConceptTypeCatalogItem>
{
    public void Configure(EntityTypeBuilder<CompensationConceptTypeCatalogItem> builder)
    {
        builder.ToTable("compensation_concept_type_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_compensation_concept_type_catalog_items");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CountryCatalogItemId).HasColumnName("country_catalog_item_id");
        builder.Property(item => item.CountryCode).HasColumnName("country_code").HasMaxLength(2);
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(80);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(80);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200);
        builder.Property(item => item.Nature).HasColumnName("nature").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.IsStatutory).HasColumnName("is_statutory");
        builder.Property(item => item.DefaultDeductionClass).HasColumnName("default_deduction_class").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.DefaultCalculationType).HasColumnName("default_calculation_type").HasConversion<string>().HasMaxLength(20);
        builder.Property(item => item.DefaultCalculationBaseCode).HasColumnName("default_calculation_base_code").HasMaxLength(40);
        builder.Property(item => item.DefaultEmployeeRate).HasColumnName("default_employee_rate").HasColumnType("numeric(11,8)");
        builder.Property(item => item.DefaultEmployerRate).HasColumnName("default_employer_rate").HasColumnType("numeric(11,8)");
        builder.Property(item => item.ContributionCap).HasColumnName("contribution_cap").HasColumnType("numeric(18,2)");
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
            .HasDatabaseName("uq_compensation_concept_type_catalog_items__public_id");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_compensation_concept_type_catalog_items__country_code");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_compensation_concept_type_catalog_items__country_active_sort");

        builder.HasIndex(item => new { item.CountryCatalogItemId, item.Nature, item.IsActive })
            .HasDatabaseName("ix_compensation_concept_type_catalog_items__country_nature_active");
    }
}
