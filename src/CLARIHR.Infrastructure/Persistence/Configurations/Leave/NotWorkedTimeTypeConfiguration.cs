using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class NotWorkedTimeTypeConfiguration : IEntityTypeConfiguration<NotWorkedTimeType>
{
    public void Configure(EntityTypeBuilder<NotWorkedTimeType> builder)
    {
        builder.ToTable("not_worked_time_types", table =>
        {
            table.HasCheckConstraint(
                "ck_not_worked_time_types__discount_percent",
                "discount_percent >= 0 and discount_percent <= 100");

            // A type that discounts must say WHERE — the domain enforces it, and so does the database.
            table.HasCheckConstraint(
                "ck_not_worked_time_types__deduction_concept",
                "discount_percent = 0 or deduction_concept_type_code is not null");
        });

        builder.HasKey(item => item.Id).HasName("pk_not_worked_time_types");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.Code).HasColumnName("code").HasMaxLength(NotWorkedTimeType.MaxCodeLength);
        builder.Property(item => item.NormalizedCode).HasColumnName("normalized_code").HasMaxLength(NotWorkedTimeType.MaxCodeLength);
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(NotWorkedTimeType.MaxNameLength);
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(NotWorkedTimeType.MaxNameLength);
        builder.Property(item => item.AppliesToPermission).HasColumnName("applies_to_permission");
        builder.Property(item => item.UsesWorkSchedule).HasColumnName("uses_work_schedule");
        builder.Property(item => item.CountsHoliday).HasColumnName("counts_holiday");
        builder.Property(item => item.CountsSaturday).HasColumnName("counts_saturday");
        builder.Property(item => item.CountsRestDay).HasColumnName("counts_rest_day");
        builder.Property(item => item.CountsSeventhDayPenalty).HasColumnName("counts_seventh_day_penalty");
        builder.Property(item => item.DiscountPercent).HasColumnName("discount_percent").HasColumnType("numeric(6,2)");
        builder.Property(item => item.DeductionConceptTypeCode)
            .HasColumnName("deduction_concept_type_code")
            .HasMaxLength(NotWorkedTimeType.MaxConceptCodeLength);
        builder.Property(item => item.IncomeConceptTypeCode)
            .HasColumnName("income_concept_type_code")
            .HasMaxLength(NotWorkedTimeType.MaxConceptCodeLength);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_not_worked_time_types__public_id");

        builder.HasIndex(item => new { item.TenantId, item.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_not_worked_time_types__tenant_code");

        builder.HasIndex(item => new { item.TenantId, item.IsActive })
            .HasDatabaseName("ix_not_worked_time_types__tenant_active");
    }
}
