using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileOneTimeDeductionConfiguration
    : IEntityTypeConfiguration<PersonnelFileOneTimeDeduction>
{
    public void Configure(EntityTypeBuilder<PersonnelFileOneTimeDeduction> builder)
    {
        builder.ToTable("personnel_file_one_time_deductions", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_one_time_deductions__amount_positive",
                "amount > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_deductions__quantity",
                "quantity IS NULL OR quantity > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_deductions__unit_value",
                "unit_value IS NULL OR unit_value > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_deductions__multiplier",
                "multiplier IS NULL OR multiplier > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_deductions__percentage",
                "percentage IS NULL OR percentage > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_deductions__base_amount",
                "base_amount IS NULL OR base_amount > 0");
            // A computed value must name its method; a fixed one must not (the components are the truth).
            table.HasCheckConstraint(
                "ck_pf_one_time_deductions__fixed_or_method",
                "is_fixed_value = true OR calculation_method IS NOT NULL");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_one_time_deductions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.DeductionDate).HasColumnName("deduction_date");
        builder.Property(item => item.Reference).HasColumnName("reference")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxReferenceLength);
        builder.Property(item => item.ConceptTypeCode).HasColumnName("concept_type_code")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxConceptTypeCodeLength).IsRequired();
        builder.Property(item => item.ConceptNameSnapshot).HasColumnName("concept_name_snapshot")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxConceptNameSnapshotLength).IsRequired();
        builder.Property(item => item.Observations).HasColumnName("observations")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxObservationsLength);

        builder.Property(item => item.IsFixedValue).HasColumnName("is_fixed_value");
        builder.Property(item => item.CalculationMethod).HasColumnName("calculation_method")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxCalculationMethodLength);
        builder.Property(item => item.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,4)");
        builder.Property(item => item.UnitValue).HasColumnName("unit_value").HasColumnType("numeric(18,4)");
        builder.Property(item => item.Multiplier).HasColumnName("multiplier").HasColumnType("numeric(18,4)");
        builder.Property(item => item.Percentage).HasColumnName("percentage").HasColumnType("numeric(18,4)");
        builder.Property(item => item.BaseAmount).HasColumnName("base_amount").HasColumnType("numeric(18,4)");
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxCurrencyCodeLength).IsRequired();

        // Plaza only — a one-time deduction carries NO cost center (P-08), unlike the one-time income.
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");

        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxRequesterNameSnapshotLength).IsRequired();

        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodPublicId).HasColumnName("payroll_period_public_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxPayrollPeriodLabelLength).IsRequired();
        builder.Property(item => item.PayrollPeriodEndDate).HasColumnName("payroll_period_end_date");

        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedUtc).HasColumnName("decided_utc");
        builder.Property(item => item.DecisionNote).HasColumnName("decision_note")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxDecisionNoteLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileOneTimeDeduction.MaxAnnulmentReasonLength);
        builder.Property(item => item.AppliedBySettlementPublicId).HasColumnName("applied_by_settlement_public_id");

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.Ignore(item => item.HasActiveApplication);

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_one_time_deductions__personnel_file");

        // Optional imputation to a company payroll-period instance (REQ-001 master); restrict delete keeps the
        // referenced period from being removed while a deduction targets it (FK real).
        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_one_time_deductions__payroll_period");

        builder.HasMany(item => item.Applications)
            .WithOne(application => application.OneTimeDeduction)
            .HasForeignKey(application => application.OneTimeDeductionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_otd_applications__one_time_deduction");

        builder.Navigation(item => item.Applications).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_one_time_deductions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_one_time_deductions__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.PayrollTypeCode })
            .HasDatabaseName("ix_pf_one_time_deductions__tenant_status_payroll");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.DeductionDate })
            .HasDatabaseName("ix_pf_one_time_deductions__tenant_status_date");
    }
}

internal sealed class PersonnelFileOneTimeDeductionApplicationConfiguration
    : IEntityTypeConfiguration<PersonnelFileOneTimeDeductionApplication>
{
    public void Configure(EntityTypeBuilder<PersonnelFileOneTimeDeductionApplication> builder)
    {
        builder.ToTable("personnel_file_one_time_deduction_applications");

        builder.HasKey(item => item.Id).HasName("pk_pf_one_time_deduction_applications");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.OneTimeDeductionId).HasColumnName("one_time_deduction_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.AppliedDate).HasColumnName("applied_date");
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileOneTimeDeductionApplication.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodPublicId).HasColumnName("payroll_period_public_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileOneTimeDeductionApplication.MaxPayrollPeriodLabelLength);
        builder.Property(item => item.OriginCode).HasColumnName("origin_code")
            .HasMaxLength(PersonnelFileOneTimeDeductionApplication.MaxOriginCodeLength).IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileOneTimeDeductionApplication.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.AppliedByUserId).HasColumnName("applied_by_user_id");
        builder.Property(item => item.SettlementPublicId).HasColumnName("settlement_public_id");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileOneTimeDeductionApplication.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileOneTimeDeductionApplication.MaxNotesLength);

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        // The parent relationship is configured on the aggregate side (HasMany) above.

        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_otd_applications__payroll_period");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_otd_applications__public_id");

        // At most ONE active application per deduction: the annulment (the REVERSAL) sets is_active=false +
        // status ANULADA, so a new application can be registered. The domain guard is the primary check; this
        // filtered-unique index closes the concurrency race.
        builder.HasIndex(
                item => new { item.TenantId, item.OneTimeDeductionId },
                "uq_pf_otd_applications__deduction_active")
            .IsUnique()
            .HasFilter("is_active");
        builder.HasIndex(
                item => new { item.TenantId, item.OneTimeDeductionId },
                "ix_pf_otd_applications__deduction");
        builder.HasIndex(item => new { item.TenantId, item.PayrollTypeCode, item.AppliedDate })
            .HasDatabaseName("ix_pf_otd_applications__payroll_applied_date");
    }
}
