using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileCompensatoryTimeCreditConfiguration
    : IEntityTypeConfiguration<PersonnelFileCompensatoryTimeCredit>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCompensatoryTimeCredit> builder)
    {
        builder.ToTable("personnel_file_compensatory_time_credits", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_comp_time_credits__hours_worked_positive",
                "hours_worked > 0");
            table.HasCheckConstraint(
                "ck_pf_comp_time_credits__hours_credited_positive",
                "hours_credited > 0");
            table.HasCheckConstraint(
                "ck_pf_comp_time_credits__factor_applied_positive",
                "factor_applied > 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_compensatory_time_credits");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CompensatoryTimeTypeId).HasColumnName("compensatory_time_type_id");
        builder.Property(item => item.TypeNameSnapshot).HasColumnName("type_name_snapshot")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxTypeNameSnapshotLength).IsRequired();
        builder.Property(item => item.WorkDate).HasColumnName("work_date");
        builder.Property(item => item.StartTime).HasColumnName("start_time");
        builder.Property(item => item.EndTime).HasColumnName("end_time");
        builder.Property(item => item.HoursWorked).HasColumnName("hours_worked").HasColumnType("numeric(5,2)");
        builder.Property(item => item.FactorApplied).HasColumnName("factor_applied").HasColumnType("numeric(5,2)");
        builder.Property(item => item.HoursCredited).HasColumnName("hours_credited").HasColumnType("numeric(6,2)");
        builder.Property(item => item.IsOverridden).HasColumnName("is_overridden");
        builder.Property(item => item.OverrideNote).HasColumnName("override_note")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxOverrideNoteLength);
        builder.Property(item => item.WorkDetail).HasColumnName("work_detail")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxWorkDetailLength).IsRequired();
        builder.Property(item => item.AuthorizedByText).HasColumnName("authorized_by_text")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxAuthorizedByTextLength).IsRequired();
        builder.Property(item => item.AuthorizerFilePublicId).HasColumnName("authorizer_file_public_id");
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.OvertimeRecordPublicId).HasColumnName("overtime_record_public_id");
        builder.Property(item => item.RegisteredByUserId).HasColumnName("registered_by_user_id")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxRegisteredByUserIdLength).IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxRegisteredByUserIdLength);
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileCompensatoryTimeCredit.MaxNotesLength);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_comp_time_credits__personnel_file");

        // Hard FK to the type master; the name/factor snapshot preserves history, restrict keeps the
        // referenced type from being deleted while in use.
        builder.HasOne(item => item.CompensatoryTimeType)
            .WithMany()
            .HasForeignKey(item => item.CompensatoryTimeTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_comp_time_credits__type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_comp_time_credits__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_comp_time_credits__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.WorkDate })
            .HasDatabaseName("ix_pf_comp_time_credits__tenant_work_date");
    }
}

internal sealed class PersonnelFileCompensatoryTimeCreditDocumentConfiguration
    : IEntityTypeConfiguration<PersonnelFileCompensatoryTimeCreditDocument>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCompensatoryTimeCreditDocument> builder)
    {
        builder.ToTable("personnel_file_compensatory_time_credit_documents");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_compensatory_time_credit_documents");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.CreditId).HasColumnName("credit_id");
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

        builder.HasOne(item => item.Credit)
            .WithMany()
            .HasForeignKey(item => item.CreditId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_comp_time_credit_docs__credit");

        // Document-type classification is OPTIONAL (nullable FK); restrict delete keeps the catalog item
        // from being removed while referenced.
        builder.HasOne(item => item.DocumentTypeCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.DocumentTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_comp_time_credit_docs__document_type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_comp_time_credit_docs__public_id");
        builder.HasIndex(item => new { item.TenantId, item.CreditId, item.IsActive })
            .HasDatabaseName("ix_pf_comp_time_credit_docs__tenant_credit_active");
        builder.HasIndex(item => item.DocumentTypeCatalogItemId)
            .HasDatabaseName("ix_pf_comp_time_credit_docs__document_type");
        builder.HasIndex(item => item.FilePublicId)
            .HasDatabaseName("ix_pf_comp_time_credit_docs__file_public_id");
    }
}

internal sealed class PersonnelFileCompensatoryTimeAbsenceConfiguration
    : IEntityTypeConfiguration<PersonnelFileCompensatoryTimeAbsence>
{
    public void Configure(EntityTypeBuilder<PersonnelFileCompensatoryTimeAbsence> builder)
    {
        builder.ToTable("personnel_file_compensatory_time_absences", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_comp_time_absences__hours_debited_positive",
                "hours_debited > 0");
            table.HasCheckConstraint(
                "ck_pf_comp_time_absences__dates",
                "start_date <= end_date");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_compensatory_time_absences");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.CompensatoryTimeTypeId).HasColumnName("compensatory_time_type_id");
        builder.Property(item => item.TypeNameSnapshot).HasColumnName("type_name_snapshot")
            .HasMaxLength(PersonnelFileCompensatoryTimeAbsence.MaxTypeNameSnapshotLength).IsRequired();
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.HoursDebited).HasColumnName("hours_debited").HasColumnType("numeric(6,2)");
        builder.Property(item => item.Reason).HasColumnName("reason")
            .HasMaxLength(PersonnelFileCompensatoryTimeAbsence.MaxReasonLength).IsRequired();
        builder.Property(item => item.PayrollPeriodPublicId).HasColumnName("payroll_period_public_id");
        builder.Property(item => item.RegisteredByUserId).HasColumnName("registered_by_user_id")
            .HasMaxLength(PersonnelFileCompensatoryTimeAbsence.MaxRegisteredByUserIdLength).IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileCompensatoryTimeAbsence.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id")
            .HasMaxLength(PersonnelFileCompensatoryTimeAbsence.MaxRegisteredByUserIdLength);
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileCompensatoryTimeAbsence.MaxNotesLength);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_comp_time_absences__personnel_file");

        builder.HasOne(item => item.CompensatoryTimeType)
            .WithMany()
            .HasForeignKey(item => item.CompensatoryTimeTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_comp_time_absences__type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_comp_time_absences__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_comp_time_absences__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StartDate, item.EndDate })
            .HasDatabaseName("ix_pf_comp_time_absences__tenant_file_dates");
    }
}
