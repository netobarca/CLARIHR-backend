using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileOvertimeRecordConfiguration
    : IEntityTypeConfiguration<PersonnelFileOvertimeRecord>
{
    public void Configure(EntityTypeBuilder<PersonnelFileOvertimeRecord> builder)
    {
        builder.ToTable("personnel_file_overtime_records", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_overtime_records__duration_minutes",
                "duration_minutes BETWEEN 0 AND 59");
            table.HasCheckConstraint(
                "ck_pf_overtime_records__duration_positive",
                "(duration_hours * 60 + duration_minutes) > 0");
            table.HasCheckConstraint(
                "ck_pf_overtime_records__factor_applied",
                "factor_applied > 0");
            table.HasCheckConstraint(
                "ck_pf_overtime_records__type_factor",
                "type_factor_snapshot > 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_overtime_records");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.WorkDate).HasColumnName("work_date");
        builder.Property(item => item.OvertimeTypePublicId).HasColumnName("overtime_type_public_id");
        builder.Property(item => item.OvertimeTypeCodeSnapshot).HasColumnName("overtime_type_code_snapshot")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxOvertimeTypeCodeSnapshotLength).IsRequired();
        builder.Property(item => item.OvertimeTypeNameSnapshot).HasColumnName("overtime_type_name_snapshot")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxOvertimeTypeNameSnapshotLength).IsRequired();
        builder.Property(item => item.TypeFactorSnapshot).HasColumnName("type_factor_snapshot").HasColumnType("numeric(5,2)");
        builder.Property(item => item.FactorApplied).HasColumnName("factor_applied").HasColumnType("numeric(5,2)");
        builder.Property(item => item.FactorOverrideNote).HasColumnName("factor_override_note")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxFactorOverrideNoteLength);
        builder.Property(item => item.DurationHours).HasColumnName("duration_hours");
        builder.Property(item => item.DurationMinutes).HasColumnName("duration_minutes");
        builder.Property(item => item.DurationDecimalHours).HasColumnName("duration_decimal_hours").HasColumnType("numeric(6,2)");
        builder.Property(item => item.StartTime).HasColumnName("start_time");
        builder.Property(item => item.EndTime).HasColumnName("end_time");

        builder.Property(item => item.JustificationTypePublicId).HasColumnName("justification_type_public_id");
        builder.Property(item => item.JustificationCodeSnapshot).HasColumnName("justification_code_snapshot")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxJustificationCodeSnapshotLength).IsRequired();
        builder.Property(item => item.JustificationNameSnapshot).HasColumnName("justification_name_snapshot")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxJustificationNameSnapshotLength).IsRequired();
        builder.Property(item => item.Observations).HasColumnName("observations")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxObservationsLength);

        builder.Property(item => item.OriginChannel).HasColumnName("origin_channel")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxOriginChannelLength).IsRequired();

        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");

        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxRequesterNameSnapshotLength).IsRequired();
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id");

        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodPublicId).HasColumnName("payroll_period_public_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxPayrollPeriodLabelLength).IsRequired();
        builder.Property(item => item.PayrollPeriodEndDate).HasColumnName("payroll_period_end_date");

        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedUtc).HasColumnName("decided_utc");
        builder.Property(item => item.DecisionNote).HasColumnName("decision_note")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxDecisionNoteLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileOvertimeRecord.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledBySettlementPublicId).HasColumnName("annulled_by_settlement_public_id");
        builder.Property(item => item.AppliedBySettlementPublicId).HasColumnName("applied_by_settlement_public_id");
        builder.Property(item => item.CompensatedByCreditPublicId).HasColumnName("compensated_by_credit_public_id");

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_overtime_records__personnel_file");

        // Optional imputation to a company payroll-period instance (REQ-001 master); restrict delete keeps the
        // referenced period from being removed while a record points at it (§0.14, FK real).
        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_overtime_records__payroll_period");

        builder.HasMany(item => item.Applications)
            .WithOne(application => application.OvertimeRecord)
            .HasForeignKey(application => application.OvertimeRecordId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_otr_applications__overtime_record");

        builder.Navigation(item => item.Applications).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_overtime_records__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_overtime_records__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.WorkDate })
            .HasDatabaseName("ix_pf_overtime_records__tenant_status_work_date");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.PayrollTypeCode })
            .HasDatabaseName("ix_pf_overtime_records__tenant_status_payroll");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.WorkDate })
            .HasDatabaseName("ix_pf_overtime_records__tenant_file_work_date");
    }
}

internal sealed class PersonnelFileOvertimeRecordApplicationConfiguration
    : IEntityTypeConfiguration<PersonnelFileOvertimeRecordApplication>
{
    public void Configure(EntityTypeBuilder<PersonnelFileOvertimeRecordApplication> builder)
    {
        builder.ToTable("personnel_file_overtime_record_applications");

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_overtime_record_applications");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.OvertimeRecordId).HasColumnName("overtime_record_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.AppliedDate).HasColumnName("applied_date");
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileOvertimeRecordApplication.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodPublicId).HasColumnName("payroll_period_public_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileOvertimeRecordApplication.MaxPayrollPeriodLabelLength);
        builder.Property(item => item.OriginCode).HasColumnName("origin_code")
            .HasMaxLength(PersonnelFileOvertimeRecordApplication.MaxOriginCodeLength).IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileOvertimeRecordApplication.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.AppliedByUserId).HasColumnName("applied_by_user_id");
        builder.Property(item => item.SettlementPublicId).HasColumnName("settlement_public_id");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileOvertimeRecordApplication.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileOvertimeRecordApplication.MaxNotesLength);

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        // The overtime-record parent relationship is configured on the aggregate side (HasMany) above.

        // Optional imputation to a company payroll-period instance (REQ-001 master); restrict delete keeps the
        // referenced period from being removed while an application points at it (§0.14, FK real).
        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_otr_applications__payroll_period");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_otr_applications__public_id");

        // At most ONE active application per record (RN-06/№11): the annulment sets is_active=false + status
        // ANULADA, so a new application can be registered. The domain guard is the primary check; this
        // filtered-unique index closes the concurrency race (the anti-duplicate final net). The named-index
        // overload lets it coexist with the plain history index below on the same column set.
        builder.HasIndex(
                item => new { item.TenantId, item.OvertimeRecordId },
                "uq_pf_otr_applications__record_active")
            .IsUnique()
            .HasFilter("is_active");
        builder.HasIndex(
                item => new { item.TenantId, item.OvertimeRecordId },
                "ix_pf_otr_applications__record");
        builder.HasIndex(item => new { item.TenantId, item.PayrollTypeCode, item.AppliedDate })
            .HasDatabaseName("ix_pf_otr_applications__payroll_applied_date");
    }
}
