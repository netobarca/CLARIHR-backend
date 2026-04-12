using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelReferenceCatalogItemConfiguration : IEntityTypeConfiguration<PersonnelReferenceCatalogItem>
{
    public void Configure(EntityTypeBuilder<PersonnelReferenceCatalogItem> builder)
    {
        builder.ToTable("personnel_reference_catalog_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_personnel_reference_catalog_items");

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.PublicId)
            .HasColumnName("public_id");

        builder.Property(item => item.CountryCode)
            .HasColumnName("country_code")
            .HasMaxLength(2);

        builder.Property(item => item.Category)
            .HasColumnName("category")
            .HasMaxLength(80);

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(120);

        builder.Property(item => item.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(120);

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(item => item.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(150);

        builder.Property(item => item.ParentId)
            .HasColumnName("parent_id");

        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property<DateTime>("CreatedUtc")
            .HasColumnName("created_utc");

        builder.Property<DateTime?>("ModifiedUtc")
            .HasColumnName("modified_utc");

        builder.HasOne(item => item.Parent)
            .WithMany()
            .HasForeignKey(item => item.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_reference_catalog_items__parent");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_reference_catalog_items__public_id");

        builder.HasIndex(item => new { item.CountryCode, item.Category, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_personnel_reference_catalog_items__country_category_code");

        builder.HasIndex(item => new { item.CountryCode, item.Category, item.ParentId, item.IsActive, item.SortOrder })
            .HasDatabaseName("ix_personnel_reference_catalog_items__country_category_parent_active_sort");

        builder.HasData(PersonnelReferenceCatalog.Items.Select(static item => new
        {
            Id = item.Id,
            PublicId = PersonnelReferenceCatalogItem.Create(item).PublicId,
            CountryCode = item.CountryCode,
            Category = item.Category,
            Code = item.Code,
            NormalizedCode = item.Code.ToUpperInvariant(),
            Name = item.Name,
            NormalizedName = item.Name.ToUpperInvariant(),
            ParentId = item.ParentId,
            SortOrder = item.SortOrder,
            IsActive = true,
            CreatedUtc = GlobalCatalogSeedData.SeededAtUtc,
            ModifiedUtc = (DateTime?)GlobalCatalogSeedData.SeededAtUtc
        }));
    }
}
