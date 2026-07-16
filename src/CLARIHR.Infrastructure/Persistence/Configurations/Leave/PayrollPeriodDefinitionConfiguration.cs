using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class PayrollPeriodDefinitionConfiguration : IEntityTypeConfiguration<PayrollPeriodDefinition>
{
    public void Configure(EntityTypeBuilder<PayrollPeriodDefinition> builder)
    {
        builder.ToTable("payroll_period_definitions", table =>
        {
            table.HasCheckConstraint(
                "ck_payroll_period_definitions__dates",
                "end_date >= start_date");
            table.HasCheckConstraint(
                "ck_payroll_period_definitions__cutoff_in_range",
                "cutoff_date IS NULL OR (cutoff_date >= start_date AND cutoff_date <= end_date)");
            table.HasCheckConstraint(
                "ck_payroll_period_definitions__month",
                "month IS NULL OR (month >= 1 AND month <= 12)");
            table.HasCheckConstraint(
                "ck_payroll_period_definitions__overtime_window",
                "overtime_entry_start IS NULL OR overtime_entry_end IS NULL OR overtime_entry_end >= overtime_entry_start");
            table.HasCheckConstraint(
                "ck_payroll_period_definitions__attendance_window",
                "attendance_entry_start IS NULL OR attendance_entry_end IS NULL OR attendance_entry_end >= attendance_entry_start");
        });

        builder.HasKey(period => period.Id)
            .HasName("pk_payroll_period_definitions");

        builder.Property(period => period.Id)
            .HasColumnName("id");

        builder.Property(period => period.PublicId)
            .HasColumnName("public_id");

        builder.Property(period => period.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(period => period.PayPeriodTypeCode)
            .HasColumnName("pay_period_type_code")
            .HasMaxLength(PayrollPeriodDefinition.MaxPayPeriodTypeCodeLength);

        builder.Property(period => period.Year)
            .HasColumnName("year");

        builder.Property(period => period.Number)
            .HasColumnName("number");

        builder.Property(period => period.Label)
            .HasColumnName("label")
            .HasMaxLength(PayrollPeriodDefinition.MaxLabelLength);

        builder.Property(period => period.StartDate)
            .HasColumnName("start_date");

        builder.Property(period => period.EndDate)
            .HasColumnName("end_date");

        builder.Property(period => period.Code)
            .HasColumnName("code")
            .HasMaxLength(PayrollPeriodDefinition.MaxCodeLength);

        builder.Property(period => period.PayrollDefinitionId)
            .HasColumnName("payroll_definition_id");

        builder.Property(period => period.CutoffDate)
            .HasColumnName("cutoff_date");

        builder.Property(period => period.PaymentDate)
            .HasColumnName("payment_date");

        builder.Property(period => period.Month)
            .HasColumnName("month");

        builder.Property(period => period.AllowsOvertimeEntry)
            .HasColumnName("allows_overtime_entry")
            .HasDefaultValue(false);

        builder.Property(period => period.OvertimeEntryStart)
            .HasColumnName("overtime_entry_start");

        builder.Property(period => period.OvertimeEntryEnd)
            .HasColumnName("overtime_entry_end");

        builder.Property(period => period.AllowsAttendance)
            .HasColumnName("allows_attendance")
            .HasDefaultValue(false);

        builder.Property(period => period.AttendanceEntryStart)
            .HasColumnName("attendance_entry_start");

        builder.Property(period => period.AttendanceEntryEnd)
            .HasColumnName("attendance_entry_end");

        builder.Property(period => period.StatusCode)
            .HasColumnName("status_code")
            .HasMaxLength(PayrollPeriodDefinition.MaxStatusCodeLength)
            .HasDefaultValue(CLARIHR.Domain.Payroll.PayrollPeriodStatuses.Generado);

        builder.HasOne<CLARIHR.Domain.Payroll.PayrollDefinition>()
            .WithMany()
            .HasForeignKey(period => period.PayrollDefinitionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_payroll_period_definitions__payroll_definition");

        builder.Property(period => period.IsActive)
            .HasColumnName("is_active");

        builder.Property(period => period.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(period => period.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(period => period.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(period => period.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_payroll_period_definitions__public_id");

        // Partial since REQ-012 M2: it keeps guarding the LEGACY rows (no Nómina) exactly as before, while
        // periods hanging from a Nómina are guarded per-definition below — two Nóminas of the same
        // frequency may each own the same (year, number).
        builder.HasIndex(period => new { period.TenantId, period.PayPeriodTypeCode, period.Year, period.Number })
            .IsUnique()
            .HasFilter("payroll_definition_id IS NULL")
            .HasDatabaseName(LeaveMasterConstraintNames.PayrollPeriodUnique);

        builder.HasIndex(period => new { period.TenantId, period.PayrollDefinitionId, period.Year, period.Number })
            .IsUnique()
            .HasFilter("payroll_definition_id IS NOT NULL")
            .HasDatabaseName(LeaveMasterConstraintNames.PayrollPeriodDefinitionScopedUnique);

        builder.HasIndex(period => new { period.TenantId, period.StartDate })
            .HasDatabaseName("ix_payroll_period_definitions__tenant_start");
    }
}
