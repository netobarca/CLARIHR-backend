using CLARIHR.Domain.LegalRepresentatives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.LegalRepresentatives;

internal sealed class LegalRepresentativeConfiguration : IEntityTypeConfiguration<LegalRepresentative>
{
    public void Configure(EntityTypeBuilder<LegalRepresentative> builder)
    {
        builder.ToTable(
            "legal_representatives",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_legal_representatives__effective_dates",
                "effective_to is null or effective_to >= effective_from"));

        builder.HasKey(legalRepresentative => legalRepresentative.Id)
            .HasName("pk_legal_representatives");

        builder.Property(legalRepresentative => legalRepresentative.Id)
            .HasColumnName("id");

        builder.Property(legalRepresentative => legalRepresentative.PublicId)
            .HasColumnName("public_id");

        builder.Property(legalRepresentative => legalRepresentative.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(legalRepresentative => legalRepresentative.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100);

        builder.Property(legalRepresentative => legalRepresentative.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100);

        builder.Property(legalRepresentative => legalRepresentative.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(201);

        builder.Property(legalRepresentative => legalRepresentative.NormalizedFullName)
            .HasColumnName("normalized_full_name")
            .HasMaxLength(201);

        builder.Property(legalRepresentative => legalRepresentative.DocumentType)
            .HasColumnName("document_type")
            .HasMaxLength(40);

        builder.Property(legalRepresentative => legalRepresentative.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(80);

        builder.Property(legalRepresentative => legalRepresentative.NormalizedDocumentNumber)
            .HasColumnName("normalized_document_number")
            .HasMaxLength(80);

        builder.Property(legalRepresentative => legalRepresentative.PositionTitle)
            .HasColumnName("position_title")
            .HasMaxLength(150);

        builder.Property(legalRepresentative => legalRepresentative.RepresentationType)
            .HasColumnName("representation_type")
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(legalRepresentative => legalRepresentative.AuthorityDescription)
            .HasColumnName("authority_description")
            .HasMaxLength(500);

        builder.Property(legalRepresentative => legalRepresentative.AppointmentInstrument)
            .HasColumnName("appointment_instrument")
            .HasMaxLength(500);

        builder.Property(legalRepresentative => legalRepresentative.AppointmentDate)
            .HasColumnName("appointment_date");

        builder.Property(legalRepresentative => legalRepresentative.EffectiveFrom)
            .HasColumnName("effective_from");

        builder.Property(legalRepresentative => legalRepresentative.EffectiveTo)
            .HasColumnName("effective_to");

        builder.Property(legalRepresentative => legalRepresentative.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(legalRepresentative => legalRepresentative.Phone)
            .HasColumnName("phone")
            .HasMaxLength(40);

        // B-04 — `is_primary` era anulable y el `null` era un tercer estado que nadie más reconocía: el índice
        // único parcial de abajo filtra por `is_primary = true` y `Inactivate()` escribe `false`, nunca `null`.
        builder.Property(legalRepresentative => legalRepresentative.IsPrimary)
            .HasColumnName("is_primary")
            .IsRequired();

        builder.Property(legalRepresentative => legalRepresentative.IsActive)
            .HasColumnName("is_active");

        builder.Property(legalRepresentative => legalRepresentative.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(legalRepresentative => legalRepresentative.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(legalRepresentative => legalRepresentative.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(legalRepresentative => legalRepresentative.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_legal_representatives__public_id");

        builder.HasIndex(legalRepresentative => new
            {
                legalRepresentative.TenantId,
                legalRepresentative.DocumentType,
                legalRepresentative.NormalizedDocumentNumber
            })
            .IsUnique()
            .HasDatabaseName("uq_legal_representatives__tenant_document_type_number");

        builder.HasIndex(legalRepresentative => new { legalRepresentative.TenantId, legalRepresentative.IsPrimary, legalRepresentative.IsActive })
            .IsUnique()
            .HasFilter("is_primary = true and is_active = true")
            .HasDatabaseName("ux_legal_representatives__tenant_primary_active");

        builder.HasIndex(legalRepresentative => new { legalRepresentative.TenantId, legalRepresentative.IsActive })
            .HasDatabaseName("ix_legal_representatives__tenant_active");

        builder.HasIndex(legalRepresentative => new { legalRepresentative.TenantId, legalRepresentative.IsPrimary })
            .HasDatabaseName("ix_legal_representatives__tenant_primary");

        builder.HasIndex(legalRepresentative => new
            {
                legalRepresentative.TenantId,
                legalRepresentative.RepresentationType,
                legalRepresentative.IsActive
            })
            .HasDatabaseName("ix_legal_representatives__tenant_representation_active");

        builder.HasIndex(legalRepresentative => new { legalRepresentative.TenantId, legalRepresentative.NormalizedFullName })
            .HasDatabaseName("ix_legal_representatives__tenant_normalized_name");

        builder.HasIndex(legalRepresentative => new { legalRepresentative.TenantId, legalRepresentative.EffectiveFrom, legalRepresentative.EffectiveTo })
            .HasDatabaseName("ix_legal_representatives__tenant_effective_dates");
    }
}
