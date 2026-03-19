using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.LegalRepresentatives;

internal sealed class LegalRepresentativeDocumentTypeCatalogItemConfiguration : IEntityTypeConfiguration<LegalRepresentativeDocumentTypeCatalogItem>
{
    public void Configure(EntityTypeBuilder<LegalRepresentativeDocumentTypeCatalogItem> builder)
    {
        builder.ToTable("legal_representative_document_type_catalog");

        builder.HasKey(item => item.Id)
            .HasName("pk_legal_representative_document_type_catalog");

        builder.Property(item => item.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(item => item.Code)
            .HasColumnName("code")
            .HasMaxLength(40);

        builder.Property(item => item.Name)
            .HasColumnName("name")
            .HasMaxLength(150);

        builder.Property(item => item.SortOrder)
            .HasColumnName("sort_order");

        builder.Property(item => item.IsActive)
            .HasColumnName("is_active");

        builder.HasIndex(item => item.Code)
            .IsUnique()
            .HasDatabaseName("uq_legal_representative_document_type_catalog__code");

        builder.HasData(LegalRepresentativeDocumentTypeCatalog.Items.Select(static item => new
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            SortOrder = item.SortOrder,
            IsActive = true,
            CreatedUtc = GlobalCatalogSeedData.SeededAtUtc,
            ModifiedUtc = GlobalCatalogSeedData.SeededAtUtc
        }));
    }
}
