using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileRecognitionConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecognition>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecognition> builder)
    {
        builder.ToTable("personnel_file_recognitions", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_recognitions__amount_positive",
                "amount IS NULL OR amount > 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_recognitions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.RecognitionTypeId).HasColumnName("recognition_type_id");
        builder.Property(item => item.TypeNameSnapshot).HasColumnName("type_name_snapshot")
            .HasMaxLength(PersonnelFileRecognition.MaxTypeNameSnapshotLength).IsRequired();
        builder.Property(item => item.EventDate).HasColumnName("event_date");
        builder.Property(item => item.Detail).HasColumnName("detail")
            .HasMaxLength(PersonnelFileRecognition.MaxDetailLength).IsRequired();
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileRecognition.MaxCurrencyCodeLength);
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.RegisteredByUserId).HasColumnName("registered_by_user_id").IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedUtc).HasColumnName("decided_utc");
        builder.Property(item => item.DecisionNote).HasColumnName("decision_note")
            .HasMaxLength(PersonnelFileRecognition.MaxDecisionNoteLength);
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileRecognition.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.PersonnelActionPublicId).HasColumnName("personnel_action_public_id");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileRecognition.MaxNotesLength);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_recognitions__personnel_file");

        // Hard FK to the type master; the name snapshot preserves history, restrict keeps the referenced
        // type from being deleted while in use.
        builder.HasOne(item => item.RecognitionType)
            .WithMany()
            .HasForeignKey(item => item.RecognitionTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_recognitions__recognition_type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_recognitions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_recognitions__tenant_file_status");
    }
}

internal sealed class PersonnelFileRecognitionDocumentConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecognitionDocument>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecognitionDocument> builder)
    {
        builder.ToTable("personnel_file_recognition_documents");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_recognition_documents");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.RecognitionId).HasColumnName("recognition_id");
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

        builder.HasOne(item => item.Recognition)
            .WithMany()
            .HasForeignKey(item => item.RecognitionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_recognition_docs__recognition");

        builder.HasOne(item => item.DocumentTypeCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.DocumentTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_recognition_docs__document_type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_recognition_docs__public_id");
        builder.HasIndex(item => new { item.TenantId, item.RecognitionId, item.IsActive })
            .HasDatabaseName("ix_pf_recognition_docs__tenant_recog_active");
        builder.HasIndex(item => item.DocumentTypeCatalogItemId)
            .HasDatabaseName("ix_pf_recognition_docs__document_type");
        builder.HasIndex(item => item.FilePublicId)
            .HasDatabaseName("ix_pf_recognition_docs__file_public_id");
    }
}

internal sealed class PersonnelFileDisciplinaryActionConfiguration
    : IEntityTypeConfiguration<PersonnelFileDisciplinaryAction>
{
    public void Configure(EntityTypeBuilder<PersonnelFileDisciplinaryAction> builder)
    {
        builder.ToTable("personnel_file_disciplinary_actions", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_disc_actions__deduction_amount_positive",
                "deduction_amount IS NULL OR deduction_amount > 0");
            table.HasCheckConstraint(
                "ck_pf_disc_actions__suspension_dates",
                "suspension_start_date IS NULL OR suspension_end_date IS NULL OR suspension_start_date <= suspension_end_date");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_disciplinary_actions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.DisciplinaryActionTypeId).HasColumnName("disciplinary_action_type_id");
        builder.Property(item => item.TypeNameSnapshot).HasColumnName("type_name_snapshot")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxTypeNameSnapshotLength).IsRequired();
        builder.Property(item => item.TypeAppliedSuspension).HasColumnName("type_applied_suspension");
        builder.Property(item => item.DisciplinaryActionCauseId).HasColumnName("disciplinary_action_cause_id");
        builder.Property(item => item.CauseNameSnapshot).HasColumnName("cause_name_snapshot")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxCauseNameSnapshotLength).IsRequired();
        builder.Property(item => item.IncidentDate).HasColumnName("incident_date");
        builder.Property(item => item.FactsDetail).HasColumnName("facts_detail")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxFactsDetailLength).IsRequired();
        builder.Property(item => item.HasPayrollDeduction).HasColumnName("has_payroll_deduction");
        builder.Property(item => item.DeductionAmount).HasColumnName("deduction_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxCurrencyCodeLength);
        builder.Property(item => item.DeductionConceptTypeCode).HasColumnName("deduction_concept_type_code")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxDeductionConceptTypeCodeLength);
        builder.Property(item => item.DeductionConceptNameSnapshot).HasColumnName("deduction_concept_name_snapshot")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxDeductionConceptNameSnapshotLength);
        builder.Property(item => item.SuspensionStartDate).HasColumnName("suspension_start_date");
        builder.Property(item => item.SuspensionEndDate).HasColumnName("suspension_end_date");
        builder.Property(item => item.SuspensionDays).HasColumnName("suspension_days");
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.RegisteredByUserId).HasColumnName("registered_by_user_id").IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedUtc).HasColumnName("decided_utc");
        builder.Property(item => item.DecisionNote).HasColumnName("decision_note")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxDecisionNoteLength);
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.PersonnelActionPublicId).HasColumnName("personnel_action_public_id");
        builder.Property(item => item.SuspensionActionPublicId).HasColumnName("suspension_action_public_id");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileDisciplinaryAction.MaxNotesLength);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_disc_actions__personnel_file");

        builder.HasOne(item => item.DisciplinaryActionType)
            .WithMany()
            .HasForeignKey(item => item.DisciplinaryActionTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_disc_actions__type");

        builder.HasOne(item => item.DisciplinaryActionCause)
            .WithMany()
            .HasForeignKey(item => item.DisciplinaryActionCauseId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_disc_actions__cause");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_disc_actions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_disc_actions__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.SuspensionStartDate, item.SuspensionEndDate })
            .HasDatabaseName("ix_pf_disc_actions__tenant_suspension_dates");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.IncidentDate })
            .HasDatabaseName("ix_pf_disc_actions__tenant_status_incident");
    }
}

internal sealed class PersonnelFileDisciplinaryActionDocumentConfiguration
    : IEntityTypeConfiguration<PersonnelFileDisciplinaryActionDocument>
{
    public void Configure(EntityTypeBuilder<PersonnelFileDisciplinaryActionDocument> builder)
    {
        builder.ToTable("personnel_file_disciplinary_action_documents");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_disciplinary_action_documents");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.DisciplinaryActionId).HasColumnName("disciplinary_action_id");
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

        builder.HasOne(item => item.DisciplinaryAction)
            .WithMany()
            .HasForeignKey(item => item.DisciplinaryActionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_disc_action_docs__disciplinary_action");

        builder.HasOne(item => item.DocumentTypeCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.DocumentTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_disc_action_docs__document_type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_disc_action_docs__public_id");
        builder.HasIndex(item => new { item.TenantId, item.DisciplinaryActionId, item.IsActive })
            .HasDatabaseName("ix_pf_disc_action_docs__tenant_action_active");
        builder.HasIndex(item => item.DocumentTypeCatalogItemId)
            .HasDatabaseName("ix_pf_disc_action_docs__document_type");
        builder.HasIndex(item => item.FilePublicId)
            .HasDatabaseName("ix_pf_disc_action_docs__file_public_id");
    }
}
