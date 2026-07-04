using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileRetirementRequestConfiguration : IEntityTypeConfiguration<PersonnelFileRetirementRequest>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRetirementRequest> builder)
    {
        builder.ToTable("personnel_file_retirement_requests");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_retirement_requests");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot").HasMaxLength(300);
        builder.Property(item => item.RequestDate).HasColumnName("request_date");
        builder.Property(item => item.RetirementDate).HasColumnName("retirement_date");
        builder.Property(item => item.RetirementCategoryCode).HasColumnName("retirement_category_code").HasMaxLength(80);
        builder.Property(item => item.RetirementCategoryNameSnapshot).HasColumnName("retirement_category_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RetirementReasonCode).HasColumnName("retirement_reason_code").HasMaxLength(80);
        builder.Property(item => item.RetirementReasonNameSnapshot).HasColumnName("retirement_reason_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(item => item.RequestStatusCode).HasColumnName("request_status_code").HasMaxLength(80);
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(item => item.ResolvedByUserId).HasColumnName("resolved_by_user_id");
        builder.Property(item => item.ResolutionDateUtc).HasColumnName("resolution_date_utc");
        builder.Property(item => item.ResolutionNotes).HasColumnName("resolution_notes").HasMaxLength(2000);
        builder.Property(item => item.CanceledByUserId).HasColumnName("canceled_by_user_id");
        builder.Property(item => item.CancellationDateUtc).HasColumnName("cancellation_date_utc");
        builder.Property(item => item.CancellationNotes).HasColumnName("cancellation_notes").HasMaxLength(2000);
        builder.Property(item => item.ExecutedByUserId).HasColumnName("executed_by_user_id");
        builder.Property(item => item.ExecutionDateUtc).HasColumnName("execution_date_utc");
        builder.Property(item => item.PriorEmploymentStatusCode).HasColumnName("prior_employment_status_code").HasMaxLength(80);
        builder.Property(item => item.PriorLoginWasActive).HasColumnName("prior_login_was_active");
        builder.Property(item => item.PriorRehireBlocked).HasColumnName("prior_rehire_blocked");
        builder.Property(item => item.PriorRehireBlockReason).HasColumnName("prior_rehire_block_reason").HasMaxLength(500);
        builder.Property(item => item.RevertedByUserId).HasColumnName("reverted_by_user_id");
        builder.Property(item => item.ReversalDateUtc).HasColumnName("reversal_date_utc");
        builder.Property(item => item.ReversalReason).HasColumnName("reversal_reason").HasMaxLength(2000);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_retirement_requests__personnel_file");

        builder.HasMany(item => item.ClosedRecords)
            .WithOne(record => record.RetirementRequest)
            .HasForeignKey(record => record.RetirementRequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_retirement_closed_records__request");

        builder.Navigation(item => item.ClosedRecords)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_retirement_requests__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.RequestStatusCode })
            .HasDatabaseName("ix_personnel_file_retirement_requests__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.RequestDate })
            .HasDatabaseName("ix_personnel_file_retirement_requests__tenant_file_date");

        // DB backup of RN-001.2 (at most ONE open request per employee): filtered unique index — the
        // handler check remains the primary guard, this closes the concurrency race (precedent:
        // uq_personnel_files__tenant_linked_user).
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId })
            .IsUnique()
            .HasFilter("request_status_code in ('SOLICITADA','AUTORIZADA') and is_active")
            .HasDatabaseName("uq_personnel_file_retirement_requests__tenant_file_open");
    }
}

internal sealed class RetirementRequestClosedRecordConfiguration : IEntityTypeConfiguration<RetirementRequestClosedRecord>
{
    public void Configure(EntityTypeBuilder<RetirementRequestClosedRecord> builder)
    {
        builder.ToTable("personnel_file_retirement_closed_records");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_retirement_closed_records");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.RetirementRequestId).HasColumnName("retirement_request_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.EntityKind).HasColumnName("entity_kind").HasMaxLength(40);
        builder.Property(item => item.EntityPublicId).HasColumnName("entity_public_id");
        builder.Property(item => item.PreviousEndDate).HasColumnName("previous_end_date");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_retirement_closed_records__public_id");
        builder.HasIndex(item => new { item.TenantId, item.RetirementRequestId })
            .HasDatabaseName("ix_personnel_file_retirement_closed_records__tenant_request");
    }
}
