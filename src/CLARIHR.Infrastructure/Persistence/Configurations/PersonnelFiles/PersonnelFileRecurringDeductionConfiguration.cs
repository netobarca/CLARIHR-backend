using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileRecurringDeductionConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecurringDeduction>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecurringDeduction> builder)
    {
        builder.ToTable("personnel_file_recurring_deductions", table =>
        {
            // A compound-interest credit carries principal + rate + count and MUST be finite; a plain one
            // carries none of them (its plan lives in the segments).
            table.HasCheckConstraint(
                "ck_pf_recurring_deductions__interest_fields",
                "uses_compound_interest = false OR (principal_amount > 0 AND interest_rate_percent > 0 AND planned_installments >= 1)");
            table.HasCheckConstraint(
                "ck_pf_recurring_deductions__interest_finite",
                "NOT (is_indefinite AND uses_compound_interest)");
            table.HasCheckConstraint(
                "ck_pf_recurring_deductions__principal_positive",
                "principal_amount IS NULL OR principal_amount > 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_recurring_deductions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.EffectiveDate).HasColumnName("effective_date");
        builder.Property(item => item.Reference).HasColumnName("reference")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxReferenceLength).IsRequired();
        builder.Property(item => item.RecurringDeductionTypeCode).HasColumnName("recurring_deduction_type_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxRecurringDeductionTypeCodeLength).IsRequired();
        builder.Property(item => item.ConceptTypeCode).HasColumnName("concept_type_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxConceptTypeCodeLength).IsRequired();
        builder.Property(item => item.ConceptNameSnapshot).HasColumnName("concept_name_snapshot")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxConceptNameSnapshotLength).IsRequired();
        builder.Property(item => item.FinancialInstitution).HasColumnName("financial_institution")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxFinancialInstitutionLength);
        builder.Property(item => item.Observations).HasColumnName("observations")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxObservationsLength);

        // Plaza only — a recurring deduction carries NO cost center (P-08), unlike the recurring income.
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");

        builder.Property(item => item.InstallmentStartDate).HasColumnName("installment_start_date");
        builder.Property(item => item.ExceptionMonthsCsv).HasColumnName("exception_months")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxExceptionMonthsCsvLength);
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxCurrencyCodeLength).IsRequired();
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.InstallmentFrequencyCode).HasColumnName("installment_frequency_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxInstallmentFrequencyCodeLength).IsRequired();
        builder.Property(item => item.ApplicationFrequencyCode).HasColumnName("application_frequency_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxApplicationFrequencyCodeLength).IsRequired();
        builder.Property(item => item.IsIndefinite).HasColumnName("is_indefinite");
        builder.Property(item => item.SettlementActionCode).HasColumnName("settlement_action_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxSettlementActionCodeLength).IsRequired();

        builder.Property(item => item.UsesCompoundInterest).HasColumnName("uses_compound_interest");
        builder.Property(item => item.PrincipalAmount).HasColumnName("principal_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.InterestRatePercent).HasColumnName("interest_rate_percent").HasColumnType("numeric(9,4)");
        builder.Property(item => item.PlannedInstallments).HasColumnName("planned_installments");

        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.RegisteredByUserId).HasColumnName("registered_by_user_id");
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedUtc).HasColumnName("decided_utc");
        builder.Property(item => item.DecisionNote).HasColumnName("decision_note")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxDecisionNoteLength);
        builder.Property(item => item.SuspendedUtc).HasColumnName("suspended_utc");
        builder.Property(item => item.SuspensionNote).HasColumnName("suspension_note")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxSuspensionNoteLength);
        builder.Property(item => item.ClosedUtc).HasColumnName("closed_utc");
        builder.Property(item => item.ClosureReason).HasColumnName("closure_reason")
            .HasMaxLength(PersonnelFileRecurringDeduction.MaxClosureReasonLength);
        builder.Property(item => item.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(item => item.ClosedBySettlementPublicId).HasColumnName("closed_by_settlement_public_id");

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        // The typed view of the exception months is decoded from the CSV column; it is not a mapped property.
        builder.Ignore(item => item.ExceptionMonths);
        builder.Ignore(item => item.PlannedInstallmentCount);

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_recurring_deductions__personnel_file");

        builder.HasMany(item => item.PlanSegments)
            .WithOne(segment => segment.RecurringDeduction)
            .HasForeignKey(segment => segment.RecurringDeductionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_rd_segments__recurring_deduction");

        builder.HasMany(item => item.Installments)
            .WithOne(installment => installment.RecurringDeduction)
            .HasForeignKey(installment => installment.RecurringDeductionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_rd_installments__recurring_deduction");

        builder.HasMany(item => item.IndebtednessOverrides)
            .WithOne(footprint => footprint.RecurringDeduction)
            .HasForeignKey(footprint => footprint.RecurringDeductionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_rd_indebt_overrides__recurring_deduction");

        builder.Navigation(item => item.PlanSegments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Installments).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.IndebtednessOverrides).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_recurring_deductions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_recurring_deductions__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.PayrollTypeCode })
            .HasDatabaseName("ix_pf_recurring_deductions__tenant_status_payroll");
    }
}

internal sealed class PersonnelFileRecurringDeductionPlanSegmentConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecurringDeductionPlanSegment>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecurringDeductionPlanSegment> builder)
    {
        builder.ToTable("personnel_file_recurring_deduction_plan_segments", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_rd_segments__from_positive",
                "from_installment >= 1");
            table.HasCheckConstraint(
                "ck_pf_rd_segments__range_ordered",
                "to_installment IS NULL OR to_installment >= from_installment");
            table.HasCheckConstraint(
                "ck_pf_rd_segments__value_positive",
                "installment_value > 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_pf_recurring_deduction_plan_segments");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.RecurringDeductionId).HasColumnName("recurring_deduction_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.FromInstallment).HasColumnName("from_installment");
        builder.Property(item => item.ToInstallment).HasColumnName("to_installment");
        builder.Property(item => item.InstallmentValue).HasColumnName("installment_value").HasColumnType("numeric(18,2)");

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_rd_segments__public_id");

        // At most ONE active segment per (credit, starting installment) — the replace-all Update rewrites the
        // whole list, so this closes the concurrency race on a double edit.
        builder.HasIndex(
                item => new { item.TenantId, item.RecurringDeductionId, item.FromInstallment },
                "uq_pf_rd_segments__deduction_from_active")
            .IsUnique()
            .HasFilter("is_active");
    }
}

internal sealed class PersonnelFileRecurringDeductionInstallmentConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecurringDeductionInstallment>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecurringDeductionInstallment> builder)
    {
        builder.ToTable("personnel_file_recurring_deduction_installments", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_rd_installments__amount_positive",
                "amount > 0");
            // The kind decides which serial the row carries: a REGULAR installment has a plan number, an
            // EXTRAORDINARIA has an E-serial — never both, never neither.
            table.HasCheckConstraint(
                "ck_pf_rd_installments__regular_number",
                "(kind = 'REGULAR') = (installment_number IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_pf_rd_installments__extraordinary_number",
                "(kind = 'EXTRAORDINARIA') = (extraordinary_number IS NOT NULL)");
            table.HasCheckConstraint(
                "ck_pf_rd_installments__capital_not_negative",
                "capital_amount IS NULL OR capital_amount >= 0");
            table.HasCheckConstraint(
                "ck_pf_rd_installments__interest_not_negative",
                "interest_amount IS NULL OR interest_amount >= 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_pf_recurring_deduction_installments");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.RecurringDeductionId).HasColumnName("recurring_deduction_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.Kind).HasColumnName("kind")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxKindLength).IsRequired();
        builder.Property(item => item.InstallmentNumber).HasColumnName("installment_number");
        builder.Property(item => item.ExtraordinaryNumber).HasColumnName("extraordinary_number");
        builder.Property(item => item.AppliedDate).HasColumnName("applied_date");
        builder.Property(item => item.TheoreticalDueDate).HasColumnName("theoretical_due_date");
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CapitalAmount).HasColumnName("capital_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.InterestAmount).HasColumnName("interest_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxCurrencyCodeLength).IsRequired();
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxPayrollPeriodLabelLength);
        builder.Property(item => item.OriginCode).HasColumnName("origin_code")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxOriginCodeLength).IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.AppliedByUserId).HasColumnName("applied_by_user_id");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileRecurringDeductionInstallment.MaxNotesLength);

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        // The parent relationship is configured on the aggregate side (HasMany) above.

        // Optional imputation to a company payroll-period instance (REQ-001 master); restrict delete keeps the
        // referenced period from being removed while an applied installment points at it (FK real).
        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_rd_installments__payroll_period");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_rd_installments__public_id");

        // At most ONE active REGULAR installment per (credit, number) and ONE active EXTRAORDINARIA per
        // (credit, serial): the annulment sets is_active=false + status ANULADA, so the same number can be
        // re-applied. The domain guard is the primary check; these filtered-unique indexes close the race.
        builder.HasIndex(
                item => new { item.TenantId, item.RecurringDeductionId, item.InstallmentNumber },
                "uq_pf_rd_installments__deduction_number_active")
            .IsUnique()
            .HasFilter("is_active AND kind = 'REGULAR'");
        builder.HasIndex(
                item => new { item.TenantId, item.RecurringDeductionId, item.ExtraordinaryNumber },
                "uq_pf_rd_installments__deduction_extra_active")
            .IsUnique()
            .HasFilter("is_active AND kind = 'EXTRAORDINARIA'");
        builder.HasIndex(
                item => new { item.TenantId, item.RecurringDeductionId, item.InstallmentNumber },
                "ix_pf_rd_installments__deduction_number");
        builder.HasIndex(item => new { item.TenantId, item.PayrollTypeCode, item.AppliedDate })
            .HasDatabaseName("ix_pf_rd_installments__payroll_applied_date");
    }
}
