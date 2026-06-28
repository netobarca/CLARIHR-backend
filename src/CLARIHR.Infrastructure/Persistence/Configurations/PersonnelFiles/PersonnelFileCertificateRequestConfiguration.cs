using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileCertificateRequestConfiguration : IEntityTypeConfiguration<PersonnelFileCertificateRequest>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCertificateRequest> builder)
    {
        builder.ToTable("personnel_file_certificate_requests");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_certificate_requests");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CertificateTypeCode).HasColumnName("certificate_type_code").HasMaxLength(80);
        builder.Property(item => item.TypeNameSnapshot).HasColumnName("type_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RequestStatusCode).HasColumnName("request_status_code").HasMaxLength(80);
        builder.Property(item => item.PurposeCode).HasColumnName("purpose_code").HasMaxLength(80);
        builder.Property(item => item.AddressedTo).HasColumnName("addressed_to").HasMaxLength(500);
        builder.Property(item => item.DeliveryMethodCode).HasColumnName("delivery_method_code").HasMaxLength(80);
        builder.Property(item => item.LanguageCode).HasColumnName("language_code").HasMaxLength(10);
        builder.Property(item => item.Copies).HasColumnName("copies");
        builder.Property(item => item.RequestDateUtc).HasColumnName("request_date_utc");
        builder.Property(item => item.NeededByDateUtc).HasColumnName("needed_by_date_utc");
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(item => item.IssuedByUserId).HasColumnName("issued_by_user_id");
        builder.Property(item => item.IssuedDateUtc).HasColumnName("issued_date_utc");
        builder.Property(item => item.DeliveredDateUtc).HasColumnName("delivered_date_utc");
        builder.Property(item => item.ResolutionNotes).HasColumnName("resolution_notes").HasMaxLength(2000);
        builder.Property(item => item.ResponseTimeDays).HasColumnName("response_time_days");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_certificate_requests__personnel_file");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_certificate_requests__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.RequestStatusCode })
            .HasDatabaseName("ix_personnel_file_certificate_requests__tenant_file_status");
        // Company-wide bandeja (D-08) filters/sorts by tenant + request date.
        builder.HasIndex(item => new { item.TenantId, item.RequestDateUtc })
            .HasDatabaseName("ix_personnel_file_certificate_requests__tenant_date");
    }
}

internal sealed class CertificateRequestDocumentConfiguration : IEntityTypeConfiguration<CertificateRequestDocument>
{
    public void Configure(EntityTypeBuilder<CertificateRequestDocument> builder)
    {
        builder.ToTable("certificate_request_documents");
        builder.HasKey(item => item.Id).HasName("pk_certificate_request_documents");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.CertificateRequestId).HasColumnName("certificate_request_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.IsSystemGenerated).HasColumnName("is_system_generated");
        builder.Property(item => item.FilePublicId).HasColumnName("file_public_id");
        builder.Property(item => item.Observations).HasColumnName("observations").HasMaxLength(2000);
        builder.Property(item => item.FileName).HasColumnName("file_name").HasMaxLength(260);
        builder.Property(item => item.ContentType).HasColumnName("content_type").HasMaxLength(200);
        builder.Property(item => item.SizeBytes).HasColumnName("size_bytes");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.CertificateRequest)
            .WithMany()
            .HasForeignKey(item => item.CertificateRequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_certificate_request_documents__request");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_certificate_request_documents__public_id");
        builder.HasIndex(item => new { item.TenantId, item.CertificateRequestId, item.IsActive })
            .HasDatabaseName("ix_certificate_request_documents__tenant_req_active");
        builder.HasIndex(item => item.FilePublicId)
            .HasDatabaseName("ix_certificate_request_documents__file_public_id");
    }
}
