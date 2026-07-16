using CLARIHR.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Payroll;

internal sealed class PayrollRunConfiguration : IEntityTypeConfiguration<PayrollRun>
{
    public void Configure(EntityTypeBuilder<PayrollRun> builder)
    {
        builder.ToTable("payroll_runs");

        builder.HasKey(run => run.Id)
            .HasName("pk_payroll_runs");

        builder.Property(run => run.Id).HasColumnName("id");
        builder.Property(run => run.PublicId).HasColumnName("public_id");
        builder.Property(run => run.TenantId).HasColumnName("tenant_id");
        builder.Property(run => run.PayrollDefinitionId).HasColumnName("payroll_definition_id");
        builder.Property(run => run.PayrollPeriodId).HasColumnName("payroll_period_id");

        builder.Property(run => run.PayrollDefinitionCode)
            .HasColumnName("payroll_definition_code")
            .HasMaxLength(PayrollRun.MaxCodeLength);

        builder.Property(run => run.PayrollDefinitionName)
            .HasColumnName("payroll_definition_name")
            .HasMaxLength(PayrollRun.MaxNameLength);

        builder.Property(run => run.PayrollTypeCode)
            .HasColumnName("payroll_type_code")
            .HasMaxLength(PayrollRun.MaxCodeLength);

        builder.Property(run => run.PeriodLabel)
            .HasColumnName("period_label")
            .HasMaxLength(PayrollRun.MaxLabelLength);

        builder.Property(run => run.PeriodStartDate).HasColumnName("period_start_date");
        builder.Property(run => run.PeriodEndDate).HasColumnName("period_end_date");
        builder.Property(run => run.PaymentDate).HasColumnName("payment_date");

        builder.Property(run => run.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(PayrollRun.CurrencyCodeLength);

        builder.Property(run => run.StatusCode)
            .HasColumnName("status_code")
            .HasMaxLength(80);

        builder.Property(run => run.GeneratedByUserId).HasColumnName("generated_by_user_id");
        builder.Property(run => run.GeneratedUtc).HasColumnName("generated_utc");
        builder.Property(run => run.RegeneratedCount).HasColumnName("regenerated_count");
        builder.Property(run => run.AuthorizedByUserId).HasColumnName("authorized_by_user_id");
        builder.Property(run => run.AuthorizedUtc).HasColumnName("authorized_utc");

        builder.Property(run => run.ReturnReason)
            .HasColumnName("return_reason")
            .HasMaxLength(PayrollRun.MaxReasonLength);

        builder.Property(run => run.ClosedByUserId).HasColumnName("closed_by_user_id");
        builder.Property(run => run.ClosedUtc).HasColumnName("closed_utc");
        builder.Property(run => run.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(run => run.AnnulledUtc).HasColumnName("annulled_utc");

        builder.Property(run => run.AnnulmentReason)
            .HasColumnName("annulment_reason")
            .HasMaxLength(PayrollRun.MaxReasonLength);

        builder.Property(run => run.EmployeeCount).HasColumnName("employee_count");
        builder.Property(run => run.TotalIncome).HasColumnName("total_income").HasColumnType("numeric(14,2)");
        builder.Property(run => run.TotalDeductions).HasColumnName("total_deductions").HasColumnType("numeric(14,2)");
        builder.Property(run => run.TotalEmployerCost).HasColumnName("total_employer_cost").HasColumnType("numeric(14,2)");
        builder.Property(run => run.TotalNet).HasColumnName("total_net").HasColumnType("numeric(14,2)");

        builder.Property(run => run.WarningsJson)
            .HasColumnName("warnings_json")
            .HasColumnType("jsonb");

        builder.Property(run => run.IsActive).HasColumnName("is_active");

        builder.Property(run => run.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(run => run.CreatedUtc).HasColumnName("created_utc");
        builder.Property(run => run.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne<PayrollDefinition>()
            .WithMany()
            .HasForeignKey(run => run.PayrollDefinitionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payroll_runs__payroll_definition");

        builder.HasOne<CLARIHR.Domain.Leave.PayrollPeriodDefinition>()
            .WithMany()
            .HasForeignKey(run => run.PayrollPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payroll_runs__payroll_period");

        builder.HasMany(run => run.Lines)
            .WithOne()
            .HasForeignKey(line => line.PayrollRunId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_payroll_run_lines__payroll_run");

        builder.Navigation(run => run.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(run => run.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_payroll_runs__public_id");

        // ONE ACTIVE run per Nómina × period (§1.4); annulment flips IsActive and releases the slot.
        builder.HasIndex(run => new { run.TenantId, run.PayrollDefinitionId, run.PayrollPeriodId })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("uq_payroll_runs__tenant_definition_period_active");

        builder.HasIndex(run => new { run.TenantId, run.StatusCode })
            .HasDatabaseName("ix_payroll_runs__tenant_status");
    }
}

internal sealed class PayrollRunLineConfiguration : IEntityTypeConfiguration<PayrollRunLine>
{
    public void Configure(EntityTypeBuilder<PayrollRunLine> builder)
    {
        builder.ToTable("payroll_run_lines", table =>
            table.HasCheckConstraint(
                "ck_payroll_run_lines__line_class",
                "line_class IN ('Ingreso','Descuento','PagoPatronal')"));

        builder.HasKey(line => line.Id)
            .HasName("pk_payroll_run_lines");

        builder.Property(line => line.Id).HasColumnName("id");
        builder.Property(line => line.PublicId).HasColumnName("public_id");
        builder.Property(line => line.TenantId).HasColumnName("tenant_id");
        builder.Property(line => line.PayrollRunId).HasColumnName("payroll_run_id");
        builder.Property(line => line.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(line => line.EmployeePublicId).HasColumnName("employee_public_id");

        builder.Property(line => line.EmployeeName)
            .HasColumnName("employee_name")
            .HasMaxLength(PayrollRunLine.MaxEmployeeNameLength);

        builder.Property(line => line.EmployeeCode)
            .HasColumnName("employee_code")
            .HasMaxLength(PayrollRunLine.MaxEmployeeCodeLength);

        builder.Property(line => line.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");

        builder.Property(line => line.CostCenterName)
            .HasColumnName("cost_center_name")
            .HasMaxLength(PayrollRunLine.MaxCostCenterNameLength);

        builder.Property(line => line.ConceptCode)
            .HasColumnName("concept_code")
            .HasMaxLength(PayrollRunLine.MaxConceptCodeLength);

        builder.Property(line => line.ConceptName)
            .HasColumnName("concept_name")
            .HasMaxLength(PayrollRunLine.MaxConceptNameLength);

        builder.Property(line => line.LineClass)
            .HasColumnName("line_class")
            .HasMaxLength(20);

        builder.Property(line => line.Units).HasColumnName("units").HasColumnType("numeric(10,2)");
        builder.Property(line => line.BaseAmount).HasColumnName("base_amount").HasColumnType("numeric(14,2)");
        builder.Property(line => line.CalculatedAmount).HasColumnName("calculated_amount").HasColumnType("numeric(14,2)");
        builder.Property(line => line.OverrideAmount).HasColumnName("override_amount").HasColumnType("numeric(14,2)");

        builder.Property(line => line.OverrideNote)
            .HasColumnName("override_note")
            .HasMaxLength(PayrollRunLine.MaxOverrideNoteLength);

        builder.Property(line => line.AdjustedByUserId).HasColumnName("adjusted_by_user_id");
        builder.Property(line => line.IsIncluded).HasColumnName("is_included").HasDefaultValue(true);

        builder.Property(line => line.SourceModule)
            .HasColumnName("source_module")
            .HasMaxLength(PayrollRunLine.MaxSourceModuleLength);

        builder.Property(line => line.SourceReferencePublicId).HasColumnName("source_reference_public_id");

        builder.Property(line => line.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(PayrollRunLine.CurrencyCodeLength);

        builder.Property(line => line.WarningCodesJson)
            .HasColumnName("warning_codes_json")
            .HasColumnType("jsonb");

        builder.Property(line => line.SortOrder).HasColumnName("sort_order");
        builder.Property(line => line.CreatedUtc).HasColumnName("created_utc");
        builder.Property(line => line.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(line => line.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_payroll_run_lines__public_id");

        builder.HasIndex(line => new { line.TenantId, line.PayrollRunId, line.PersonnelFileId })
            .HasDatabaseName("ix_payroll_run_lines__tenant_run_file");

        builder.HasIndex(line => new { line.TenantId, line.PayrollRunId, line.ConceptCode })
            .HasDatabaseName("ix_payroll_run_lines__tenant_run_concept");

        // Derived-consumption probe of the TNT/disciplinary carryover (REQ-014 P-03 — §0.11): a source
        // record is "consumed" iff an INCLUDED line of a non-annulled run references it.
        builder.HasIndex(line => new { line.TenantId, line.SourceModule, line.SourceReferencePublicId })
            .HasDatabaseName("ix_payroll_run_lines__tenant_source");

        // Employee axis across runs (REQ-015 — the per-employee payment history / open-period query).
        builder.HasIndex(line => new { line.TenantId, line.PersonnelFileId, line.PayrollRunId })
            .HasDatabaseName("ix_payroll_run_lines__tenant_file_run");
    }
}
