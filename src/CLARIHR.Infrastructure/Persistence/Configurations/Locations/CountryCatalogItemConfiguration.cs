using CLARIHR.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Locations;

internal sealed class CountryCatalogItemConfiguration : IEntityTypeConfiguration<CountryCatalogItem>
{
    public void Configure(EntityTypeBuilder<CountryCatalogItem> builder)
    {
        builder.ToTable("country_catalog");

        builder.HasKey(item => item.Id)
            .HasName("pk_country_catalog");

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.PublicId)
            .HasColumnName("public_id");

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(2);

        builder.Property(item => item.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(2);

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(item => item.DefaultLocale)
            .HasColumnName("default_locale")
            .HasMaxLength(16);

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.Property<DateTime>("CreatedUtc")
            .HasColumnName("created_utc");

        builder.Property<DateTime?>("ModifiedUtc")
            .HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_country_catalog__public_id");

        builder.HasIndex(item => item.NormalizedCode)
            .IsUnique()
            .HasDatabaseName("uq_country_catalog__normalized_code");

        builder.HasIndex(item => item.Name)
            .HasDatabaseName("ix_country_catalog__name");

        builder.HasData(CountryCatalog.Items.Select(static item => new
        {
            Id = item.Id,
            PublicId = CountryCatalogItem.Create(item).PublicId,
            Code = item.Code,
            NormalizedCode = item.Code,
            Name = item.Name,
            SortOrder = item.SortOrder,
            DefaultLocale = CountryCatalogItem.Create(item).DefaultLocale,
            IsActive = true,
            CreatedUtc = GlobalCatalogSeedData.SeededAtUtc,
            ModifiedUtc = (DateTime?)GlobalCatalogSeedData.SeededAtUtc
        }));
    }
}
