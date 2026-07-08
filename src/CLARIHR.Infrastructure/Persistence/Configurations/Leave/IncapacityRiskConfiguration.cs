using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class IncapacityRiskConfiguration : IEntityTypeConfiguration<IncapacityRisk>
{
    public void Configure(EntityTypeBuilder<IncapacityRisk> builder)
    {
        builder.ToTable("incapacity_risks");

        builder.HasKey(risk => risk.Id)
            .HasName("pk_incapacity_risks");

        builder.Property(risk => risk.Id)
            .HasColumnName("id");

        builder.Property(risk => risk.PublicId)
            .HasColumnName("public_id");

        builder.Property(risk => risk.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(risk => risk.Code)
            .HasColumnName("code")
            .HasMaxLength(IncapacityRisk.MaxCodeLength);

        builder.Property(risk => risk.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(IncapacityRisk.MaxCodeLength);

        builder.Property(risk => risk.Name)
            .HasColumnName("name")
            .HasMaxLength(IncapacityRisk.MaxNameLength);

        builder.Property(risk => risk.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(IncapacityRisk.MaxNameLength);

        builder.Property(risk => risk.CountsSeventhDay)
            .HasColumnName("counts_seventh_day");

        builder.Property(risk => risk.CountsSaturday)
            .HasColumnName("counts_saturday");

        builder.Property(risk => risk.CountsHoliday)
            .HasColumnName("counts_holiday");

        builder.Property(risk => risk.UsesWorkSchedule)
            .HasColumnName("uses_work_schedule");

        builder.Property(risk => risk.AllowsIndefinite)
            .HasColumnName("allows_indefinite");

        builder.Property(risk => risk.AllowsExtension)
            .HasColumnName("allows_extension");

        builder.Property(risk => risk.UsesFund)
            .HasColumnName("uses_fund");

        builder.Property(risk => risk.HasSubsidy)
            .HasColumnName("has_subsidy");

        builder.Property(risk => risk.IsActive)
            .HasColumnName("is_active");

        builder.Property(risk => risk.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(risk => risk.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(risk => risk.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(risk => risk.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_incapacity_risks__public_id");

        builder.HasIndex(risk => new { risk.TenantId, risk.NormalizedCode })
            .IsUnique()
            .HasDatabaseName(LeaveMasterConstraintNames.IncapacityRiskCodeUnique);

        builder.HasIndex(risk => new { risk.TenantId, risk.IsActive })
            .HasDatabaseName("ix_incapacity_risks__tenant_active");

        builder.HasMany(risk => risk.Parameters)
            .WithOne()
            .HasForeignKey(parameter => parameter.IncapacityRiskId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_incapacity_risk_parameters__incapacity_risks");

        builder.Navigation(risk => risk.Parameters).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
