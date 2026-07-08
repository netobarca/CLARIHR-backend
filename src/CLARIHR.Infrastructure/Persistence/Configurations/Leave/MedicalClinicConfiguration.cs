using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class MedicalClinicConfiguration : IEntityTypeConfiguration<MedicalClinic>
{
    public void Configure(EntityTypeBuilder<MedicalClinic> builder)
    {
        builder.ToTable("medical_clinics");

        builder.HasKey(clinic => clinic.Id)
            .HasName("pk_medical_clinics");

        builder.Property(clinic => clinic.Id)
            .HasColumnName("id");

        builder.Property(clinic => clinic.PublicId)
            .HasColumnName("public_id");

        builder.Property(clinic => clinic.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(clinic => clinic.Description)
            .HasColumnName("description")
            .HasMaxLength(MedicalClinic.MaxDescriptionLength);

        builder.Property(clinic => clinic.NormalizedDescription)
            .HasColumnName("normalized_description")
            .HasMaxLength(MedicalClinic.MaxDescriptionLength);

        builder.Property(clinic => clinic.Specialty)
            .HasColumnName("specialty")
            .HasMaxLength(MedicalClinic.MaxSpecialtyLength);

        builder.Property(clinic => clinic.SectorCode)
            .HasColumnName("sector_code")
            .HasMaxLength(MedicalClinic.MaxSectorCodeLength);

        builder.Property(clinic => clinic.IsActive)
            .HasColumnName("is_active");

        builder.Property(clinic => clinic.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(clinic => clinic.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(clinic => clinic.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(clinic => clinic.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_medical_clinics__public_id");

        builder.HasIndex(clinic => new { clinic.TenantId, clinic.NormalizedDescription })
            .IsUnique()
            .HasDatabaseName(LeaveMasterConstraintNames.MedicalClinicDescriptionUnique);

        builder.HasIndex(clinic => new { clinic.TenantId, clinic.IsActive })
            .HasDatabaseName("ix_medical_clinics__tenant_active");
    }
}
