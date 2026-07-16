using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Payroll;

internal sealed class PayrollDefinitionConfiguration : IEntityTypeConfiguration<PayrollDefinition>
{
    public void Configure(EntityTypeBuilder<PayrollDefinition> builder)
    {
        builder.ToTable("payroll_definitions", table =>
            table.HasCheckConstraint(
                "ck_payroll_definitions__total_periods",
                "total_periods >= 1"));

        builder.HasKey(definition => definition.Id)
            .HasName("pk_payroll_definitions");

        builder.Property(definition => definition.Id)
            .HasColumnName("id");

        builder.Property(definition => definition.PublicId)
            .HasColumnName("public_id");

        builder.Property(definition => definition.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(definition => definition.Code)
            .HasColumnName("code")
            .HasMaxLength(PayrollDefinition.MaxCodeLength);

        builder.Property(definition => definition.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(PayrollDefinition.MaxCodeLength);

        builder.Property(definition => definition.Name)
            .HasColumnName("name")
            .HasMaxLength(PayrollDefinition.MaxNameLength);

        builder.Property(definition => definition.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(PayrollDefinition.MaxNameLength);

        builder.Property(definition => definition.PayrollTypeCode)
            .HasColumnName("payroll_type_code")
            .HasMaxLength(PayrollDefinition.MaxPayrollTypeCodeLength);

        builder.Property(definition => definition.PayPeriodCode)
            .HasColumnName("pay_period_code")
            .HasMaxLength(PayrollDefinition.MaxPayPeriodCodeLength);

        builder.Property(definition => definition.TotalPeriods)
            .HasColumnName("total_periods");

        builder.Property(definition => definition.GuaranteesMinimumIncome)
            .HasColumnName("guarantees_minimum_income");

        builder.Property(definition => definition.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(PayrollDefinition.CurrencyCodeLength);

        builder.Property(definition => definition.OvertimeWindowEnabled)
            .HasColumnName("overtime_window_enabled");

        builder.Property(definition => definition.OvertimeWindowOffsetDays)
            .HasColumnName("overtime_window_offset_days");

        builder.Property(definition => definition.AttendanceWindowEnabled)
            .HasColumnName("attendance_window_enabled");

        builder.Property(definition => definition.AttendanceWindowOffsetDays)
            .HasColumnName("attendance_window_offset_days");

        builder.Property(definition => definition.IsActive)
            .HasColumnName("is_active");

        builder.Property(definition => definition.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(definition => definition.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(definition => definition.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(definition => definition.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_payroll_definitions__public_id");

        // Filtered unique on the active codes only (a code can be reused after inactivation) — the handlers
        // translate a 23505 on this constraint into a clean 409 conflict.
        builder.HasIndex(definition => new { definition.TenantId, definition.NormalizedCode })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName(PayrollMasterConstraintNames.PayrollDefinitionCodeUnique);

        builder.HasIndex(definition => new { definition.TenantId, definition.IsActive })
            .HasDatabaseName("ix_payroll_definitions__tenant_active");
    }
}
