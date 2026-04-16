using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgStructureCatalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("companies");

        builder.HasKey(company => company.Id)
            .HasName("pk_companies");

        builder.Property(company => company.Id)
            .HasColumnName("id");

        builder.Property(company => company.PublicId)
            .HasColumnName("public_id");

        builder.Property(company => company.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(company => company.Slug)
            .HasColumnName("slug")
            .HasMaxLength(120);

        builder.Property(company => company.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(3);

        builder.Property(company => company.CountryCatalogItemId)
            .HasColumnName("country_catalog_item_id");

        builder.Property(company => company.DefaultLocale)
            .HasColumnName("default_locale")
            .HasMaxLength(16);

        builder.Property(company => company.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(company => company.CreatedByUserPublicId)
            .HasColumnName("created_by_user_public_id");

        builder.Property(company => company.CompanyTypeCatalogItemId)
            .HasColumnName("company_type_catalog_item_id");

        builder.Property(company => company.IsBillable)
            .HasColumnName("is_billable");

        builder.Property(company => company.BillableSinceUtc)
            .HasColumnName("billable_since_utc");

        builder.Property(company => company.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(company => company.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(company => company.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_companies__public_id");

        builder.HasIndex(company => company.Slug)
            .IsUnique()
            .HasDatabaseName("uq_companies__slug");

        builder.HasIndex(company => company.CompanyTypeCatalogItemId)
            .HasDatabaseName("ix_companies__company_type_catalog_item");

        builder.HasIndex(company => company.CountryCatalogItemId)
            .HasDatabaseName("ix_companies__country_catalog_item");

        builder.HasOne<CountryCatalogItem>()
            .WithMany()
            .HasForeignKey(company => company.CountryCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_companies__country_catalog_item");

        builder.HasOne<CompanyTypeCatalogItem>()
            .WithMany()
            .HasForeignKey(company => company.CompanyTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_companies__company_type_catalog_item");
    }
}
