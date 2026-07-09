using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileOneTimeIncomeConfiguration
    : IEntityTypeConfiguration<PersonnelFileOneTimeIncome>
{
    public void Configure(EntityTypeBuilder<PersonnelFileOneTimeIncome> builder)
    {
        builder.ToTable("personnel_file_one_time_incomes", table =>
        {
            table.HasCheckConstraint(
                "ck_pf_one_time_incomes__amount_positive",
                "amount > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_incomes__quantity",
                "quantity IS NULL OR quantity > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_incomes__unit_value",
                "unit_value IS NULL OR unit_value > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_incomes__multiplier",
                "multiplier IS NULL OR multiplier > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_incomes__percentage",
                "percentage IS NULL OR percentage > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_incomes__base_amount",
                "base_amount IS NULL OR base_amount > 0");
            table.HasCheckConstraint(
                "ck_pf_one_time_incomes__fixed_or_method",
                "is_fixed_value = true OR calculation_method IS NOT NULL");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_one_time_incomes");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.IncomeDate).HasColumnName("income_date");
        builder.Property(item => item.Reference).HasColumnName("reference")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxReferenceLength);
        builder.Property(item => item.ConceptTypeCode).HasColumnName("concept_type_code")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxConceptTypeCodeLength).IsRequired();
        builder.Property(item => item.ConceptNameSnapshot).HasColumnName("concept_name_snapshot")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxConceptNameSnapshotLength).IsRequired();
        builder.Property(item => item.Observations).HasColumnName("observations")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxObservationsLength);

        builder.Property(item => item.IsFixedValue).HasColumnName("is_fixed_value");
        builder.Property(item => item.CalculationMethod).HasColumnName("calculation_method")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxCalculationMethodLength);
        builder.Property(item => item.Quantity).HasColumnName("quantity").HasColumnType("numeric(18,4)");
        builder.Property(item => item.UnitValue).HasColumnName("unit_value").HasColumnType("numeric(18,4)");
        builder.Property(item => item.Multiplier).HasColumnName("multiplier").HasColumnType("numeric(9,4)");
        builder.Property(item => item.Percentage).HasColumnName("percentage").HasColumnType("numeric(9,4)");
        builder.Property(item => item.BaseAmount).HasColumnName("base_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.Amount).HasColumnName("amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxCurrencyCodeLength).IsRequired();

        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.CostCenterPublicId).HasColumnName("cost_center_public_id");
        builder.Property(item => item.CostCenterNameSnapshot).HasColumnName("cost_center_name_snapshot")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxCostCenterNameSnapshotLength).IsRequired();

        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxRequesterNameSnapshotLength).IsRequired();

        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodPublicId).HasColumnName("payroll_period_public_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxPayrollPeriodLabelLength).IsRequired();
        builder.Property(item => item.PayrollPeriodEndDate).HasColumnName("payroll_period_end_date");

        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(item => item.DecidedUtc).HasColumnName("decided_utc");
        builder.Property(item => item.DecisionNote).HasColumnName("decision_note")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxDecisionNoteLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileOneTimeIncome.MaxAnnulmentReasonLength);
        builder.Property(item => item.AppliedBySettlementPublicId).HasColumnName("applied_by_settlement_public_id");

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_one_time_incomes__personnel_file");

        // Optional imputation to a company payroll-period instance (REQ-001 master); restrict delete keeps the
        // referenced period from being removed while an income points at it (§0.13, FK real).
        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_one_time_incomes__payroll_period");

        builder.HasMany(item => item.Applications)
            .WithOne(application => application.OneTimeIncome)
            .HasForeignKey(application => application.OneTimeIncomeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_oti_applications__one_time_income");

        builder.Navigation(item => item.Applications).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_one_time_incomes__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_pf_one_time_incomes__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.PayrollTypeCode })
            .HasDatabaseName("ix_pf_one_time_incomes__tenant_status_payroll");
        builder.HasIndex(item => new { item.TenantId, item.StatusCode, item.IncomeDate })
            .HasDatabaseName("ix_pf_one_time_incomes__tenant_status_income_date");
    }
}

internal sealed class PersonnelFileOneTimeIncomeApplicationConfiguration
    : IEntityTypeConfiguration<PersonnelFileOneTimeIncomeApplication>
{
    public void Configure(EntityTypeBuilder<PersonnelFileOneTimeIncomeApplication> builder)
    {
        builder.ToTable("personnel_file_one_time_income_applications");

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_one_time_income_applications");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.OneTimeIncomeId).HasColumnName("one_time_income_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");

        builder.Property(item => item.AppliedDate).HasColumnName("applied_date");
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code")
            .HasMaxLength(PersonnelFileOneTimeIncomeApplication.MaxPayrollTypeCodeLength).IsRequired();
        builder.Property(item => item.PayrollPeriodId).HasColumnName("payroll_period_id");
        builder.Property(item => item.PayrollPeriodPublicId).HasColumnName("payroll_period_public_id");
        builder.Property(item => item.PayrollPeriodLabel).HasColumnName("payroll_period_label")
            .HasMaxLength(PersonnelFileOneTimeIncomeApplication.MaxPayrollPeriodLabelLength);
        builder.Property(item => item.OriginCode).HasColumnName("origin_code")
            .HasMaxLength(PersonnelFileOneTimeIncomeApplication.MaxOriginCodeLength).IsRequired();
        builder.Property(item => item.StatusCode).HasColumnName("status_code")
            .HasMaxLength(PersonnelFileOneTimeIncomeApplication.MaxStatusCodeLength).IsRequired();
        builder.Property(item => item.AppliedByUserId).HasColumnName("applied_by_user_id");
        builder.Property(item => item.SettlementPublicId).HasColumnName("settlement_public_id");
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason")
            .HasMaxLength(PersonnelFileOneTimeIncomeApplication.MaxAnnulmentReasonLength);
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.Notes).HasColumnName("notes")
            .HasMaxLength(PersonnelFileOneTimeIncomeApplication.MaxNotesLength);

        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        // The one-time-income parent relationship is configured on the aggregate side (HasMany) above.

        // Optional imputation to a company payroll-period instance (REQ-001 master); restrict delete keeps the
        // referenced period from being removed while an application points at it (§0.13, FK real).
        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_pf_oti_applications__payroll_period");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_oti_applications__public_id");

        // At most ONE active application per income (RN-06): the annulment sets is_active=false + status
        // ANULADA, so a new application can be registered. The domain guard is the primary check; this
        // filtered-unique index closes the concurrency race (the anti-duplicate final net). The named-index
        // overload lets it coexist with the plain history index below on the same column set.
        builder.HasIndex(
                item => new { item.TenantId, item.OneTimeIncomeId },
                "uq_pf_oti_applications__income_active")
            .IsUnique()
            .HasFilter("is_active");
        builder.HasIndex(
                item => new { item.TenantId, item.OneTimeIncomeId },
                "ix_pf_oti_applications__income");
        builder.HasIndex(item => new { item.TenantId, item.PayrollTypeCode, item.AppliedDate })
            .HasDatabaseName("ix_pf_oti_applications__payroll_applied_date");
    }
}
