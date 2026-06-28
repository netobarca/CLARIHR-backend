using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileEconomicAidRequestConfiguration : IEntityTypeConfiguration<PersonnelFileEconomicAidRequest>
{
    public void Configure(EntityTypeBuilder<PersonnelFileEconomicAidRequest> builder)
    {
        builder.ToTable("personnel_file_economic_aid_requests");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_economic_aid_requests");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.EconomicAidTypeCode).HasColumnName("economic_aid_type_code").HasMaxLength(80);
        builder.Property(item => item.TypeNameSnapshot).HasColumnName("type_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RequestStatusCode).HasColumnName("request_status_code").HasMaxLength(80);
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(2000);
        builder.Property(item => item.RequestedAmount).HasColumnName("requested_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(40);
        builder.Property(item => item.RequestDateUtc).HasColumnName("request_date_utc");
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(item => item.ApprovedAmount).HasColumnName("approved_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(item => item.ResolutionDateUtc).HasColumnName("resolution_date_utc");
        builder.Property(item => item.ResolutionNotes).HasColumnName("resolution_notes").HasMaxLength(2000);
        builder.Property(item => item.ResponseTimeDays).HasColumnName("response_time_days");
        builder.Property(item => item.DisbursedAmount).HasColumnName("disbursed_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.DisbursementDateUtc).HasColumnName("disbursement_date_utc");
        builder.Property(item => item.PaymentMethodCode).HasColumnName("payment_method_code").HasMaxLength(80);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_economic_aid_requests__personnel_file");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_economic_aid_requests__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.RequestStatusCode })
            .HasDatabaseName("ix_personnel_file_economic_aid_requests__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.RequestDateUtc })
            .HasDatabaseName("ix_personnel_file_economic_aid_requests__tenant_file_date");
    }
}

internal sealed class EconomicAidRequestDocumentConfiguration : IEntityTypeConfiguration<EconomicAidRequestDocument>
{
    public void Configure(EntityTypeBuilder<EconomicAidRequestDocument> builder)
    {
        builder.ToTable("economic_aid_request_documents");
        builder.HasKey(item => item.Id).HasName("pk_economic_aid_request_documents");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.EconomicAidRequestId).HasColumnName("economic_aid_request_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.DocumentTypeCatalogItemId).HasColumnName("document_type_catalog_item_id");
        builder.Property(item => item.FilePublicId).HasColumnName("file_public_id");
        builder.Property(item => item.Observations).HasColumnName("observations").HasMaxLength(2000);
        builder.Property(item => item.FileName).HasColumnName("file_name").HasMaxLength(260);
        builder.Property(item => item.ContentType).HasColumnName("content_type").HasMaxLength(200);
        builder.Property(item => item.SizeBytes).HasColumnName("size_bytes");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.EconomicAidRequest)
            .WithMany()
            .HasForeignKey(item => item.EconomicAidRequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_economic_aid_request_documents__request");

        // Document-type classification is OPTIONAL (D-06 — any kind of supporting evidence), so the FK is
        // nullable; restrict delete keeps the catalog item from being removed while referenced.
        builder.HasOne(item => item.DocumentTypeCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.DocumentTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_economic_aid_request_documents__document_type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_economic_aid_request_documents__public_id");
        builder.HasIndex(item => new { item.TenantId, item.EconomicAidRequestId, item.IsActive })
            .HasDatabaseName("ix_economic_aid_request_documents__tenant_req_active");
        builder.HasIndex(item => item.DocumentTypeCatalogItemId)
            .HasDatabaseName("ix_economic_aid_request_documents__document_type");
        builder.HasIndex(item => item.FilePublicId)
            .HasDatabaseName("ix_economic_aid_request_documents__file_public_id");
    }
}
