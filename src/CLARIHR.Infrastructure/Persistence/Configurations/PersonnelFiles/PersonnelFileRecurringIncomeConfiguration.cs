using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileRecurringIncomeConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecurringIncome>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecurringIncome> builder)
    {
        builder.ToTable("personnel_file_recurring_incomes", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_recurring_incomes__installment_value",
                "installment_value > 0");
            table.HasCheckConstraint(
                "ck_pf_recurring_incomes__total_amount",
                "total_amount IS NULL OR total_amount > 0");
            table.HasCheckConstraint(
                "ck_pf_recurring_incomes__installment_count",
                "installment_count IS NULL OR installment_count >= 1");
            table.HasCheckConstraint(
                "ck_pf_recurring_incomes__indefinite_no_limits",
                "is_indefinite = false OR (installment_count IS NULL AND total_amount IS NULL)");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_recurring_incomes");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.RegistrationDate).HasColumnName("registration_date");
        builder.Property(item => item.Reference).HasColumnName("reference")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxReferenceLength);
        builder.Property(item => item.RecurringIncomeTypeCode).HasColumnName("recurring_income_type_code")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxRecurringIncomeTypeCodeLength).IsRequired();
        builder.Property(item => item.ConceptTypeCode).HasColumnName("concept_type_code")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxConceptTypeCodeLength).IsRequired();
        builder.Property(item => item.ConceptNameSnapshot).HasColumnName("concept_name_snapshot")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxConceptNameSnapshotLength).IsRequired();
        builder.Property(item => item.Observations).HasColumnName("observations")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxObservationsLength);

        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.CostCenterPublicId).HasColumnName("cost_center_public_id");
        builder.Property(item => item.CostCenterNameSnapshot).HasColumnName("cost_center_name_snapshot")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxCostCenterNameSnapshotLength).IsRequired();

        builder.Property(item => item.InstallmentStartDate).HasColumnName("installment_start_date");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxCurrencyCodeLength).IsRequired();
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.InstallmentFrequencyCode).HasColumnName("installment_frequency_code")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxInstallmentFrequencyCodeLength).IsRequired();
        builder.Property(item => item.IsIndefinite).HasColumnName("is_indefinite");
        builder.Property(item => item.InstallmentValue).HasColumnName("installment_value").HasColumnType("numeric(18,2)");
        builder.Property(item => item.InstallmentCount).HasColumnName("installment_count");
        builder.Property(item => item.TotalAmount).HasColumnName("total_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.SettlementActionCode).HasColumnName("settlement_action_code")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxSettlementActionCodeLength).IsRequired();

        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.RegisteredByUserId).HasColumnName("registered_by_user_id");
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedUtc).HasColumnName("decided_utc");
        builder.Property(item => item.DecisionNote).HasColumnName("decision_note")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxDecisionNoteLength);
        builder.Property(item => item.SuspendedUtc).HasColumnName("suspended_utc");
        builder.Property(item => item.SuspensionNote).HasColumnName("suspension_note")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxSuspensionNoteLength);
        builder.Property(item => item.ClosedUtc).HasColumnName("closed_utc");
        builder.Property(item => item.ClosureReason).HasColumnName("closure_reason")
            .HasMaxLength(PersonnelFileRecurringIncome.MaxClosureReasonLength);
        builder.Property(item => item.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(item => item.ClosedBySettlementPublicId).HasColumnName("closed_by_settlement_public_id");

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_recurring_incomes__personnel_file");

        builder.HasMany(item => item.Installments)
            .WithOne(installment => installment.RecurringIncome)
            .HasForeignKey(installment => installment.RecurringIncomeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_ri_installments__recurring_income");

        builder.Navigation(item => item.Installments).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_recurring_incomes__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_recurring_incomes__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.PayrollTypeCode })
            .HasDatabaseName("ix_pf_recurring_incomes__tenant_status_payroll");
    }
}

internal sealed class PersonnelFileRecurringIncomeInstallmentConfiguration
    : IEntityTypeConfiguration<PersonnelFileRecurringIncomeInstallment>
{
    public void Configure(EntityTypeBuilder<PersonnelFileRecurringIncomeInstallment> builder)
    {
        builder.ToTable("personnel_file_recurring_income_installments", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_ri_installments__amount_positive",
                "amount > 0");
            table.HasCheckConstraint(
                "ck_pf_ri_installments__number_positive",
                "installment_number >= 1");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_recurring_income_installments");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.RecurringIncomeId).HasColumnName("recurring_income_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.InstallmentNumber).HasColumnName("installment_number");
        builder.Property(item => item.AppliedDate).HasColumnName("applied_date");
        builder.Property(item => item.TheoreticalDueDate).HasColumnName("theoretical_due_date");
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileRecurringIncomeInstallment.MaxCurrencyCodeLength).IsRequired();
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileRecurringIncomeInstallment.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileRecurringIncomeInstallment.MaxPayrollPeriodLabelLength);
        builder.Property(item => item.OriginCode).HasColumnName("origin_code")
            .HasMaxLength(PersonnelFileRecurringIncomeInstallment.MaxOriginCodeLength).IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileRecurringIncomeInstallment.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.AppliedByUserId).HasColumnName("applied_by_user_id");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileRecurringIncomeInstallment.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileRecurringIncomeInstallment.MaxNotesLength);

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        // The recurring-income parent relationship is configured on the aggregate side (HasMany) above.

        // Optional imputation to a company payroll-period instance (REQ-001 master); restrict delete keeps the
        // referenced period from being removed while an applied installment points at it (§0.13, FK real).
        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_ri_installments__payroll_period");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_ri_installments__public_id");

        // At most ONE active installment per (income, number): the annulment sets is_active=false + status
        // ANULADA, so the same number can be re-applied (RF-008). The domain guard is the primary check; this
        // filtered-unique index closes the concurrency race (the anti-duplicate final net). The named-index
        // overload lets it coexist with the plain history index below on the same column set.
        builder.HasIndex(
                item => new { item.TenantId, item.RecurringIncomeId, item.InstallmentNumber },
                "uq_pf_ri_installments__income_number_active")
            .IsUnique()
            .HasFilter("is_active");
        builder.HasIndex(
                item => new { item.TenantId, item.RecurringIncomeId, item.InstallmentNumber },
                "ix_pf_ri_installments__income_number");
        builder.HasIndex(item => new { item.TenantId, item.PayrollTypeCode, item.AppliedDate })
            .HasDatabaseName("ix_pf_ri_installments__payroll_applied_date");
    }
}
