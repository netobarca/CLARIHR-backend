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
            table.HasCheckConstraint(
                "ck_payroll_period_definitions__dates",
                "end_date >= start_date"));

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

        builder.HasIndex(period => new { period.TenantId, period.PayPeriodTypeCode, period.Year, period.Number })
            .IsUnique()
            .HasDatabaseName(LeaveMasterConstraintNames.PayrollPeriodUnique);

        builder.HasIndex(period => new { period.TenantId, period.StartDate })
            .HasDatabaseName("ix_payroll_period_definitions__tenant_start");
    }
}
