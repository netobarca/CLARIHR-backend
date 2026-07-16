using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Domain.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Payroll;

internal sealed class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> builder)
    {
        builder.ToTable("work_schedules", table =>
            table.HasCheckConstraint(
                "ck_work_schedules__total_weekly_hours",
                "total_weekly_hours > 0 AND total_weekly_hours <= 168"));

        builder.HasKey(schedule => schedule.Id)
            .HasName("pk_work_schedules");

        builder.Property(schedule => schedule.Id)
            .HasColumnName("id");

        builder.Property(schedule => schedule.PublicId)
            .HasColumnName("public_id");

        builder.Property(schedule => schedule.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(schedule => schedule.Code)
            .HasColumnName("code")
            .HasMaxLength(WorkSchedule.MaxCodeLength);

        builder.Property(schedule => schedule.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(WorkSchedule.MaxCodeLength);

        builder.Property(schedule => schedule.Name)
            .HasColumnName("name")
            .HasMaxLength(WorkSchedule.MaxNameLength);

        builder.Property(schedule => schedule.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(WorkSchedule.MaxNameLength);

        builder.Property(schedule => schedule.ScheduleLabel)
            .HasColumnName("schedule_label")
            .HasMaxLength(WorkSchedule.MaxScheduleLabelLength);

        builder.Property(schedule => schedule.AttendanceDateAnchor)
            .HasColumnName("attendance_date_anchor")
            .HasMaxLength(20);

        builder.Property(schedule => schedule.ScheduleClass)
            .HasColumnName("schedule_class")
            .HasMaxLength(20);

        builder.Property(schedule => schedule.TotalWeeklyHours)
            .HasColumnName("total_weekly_hours")
            .HasColumnType("numeric(6,2)");

        builder.Property(schedule => schedule.IsActive)
            .HasColumnName("is_active");

        builder.Property(schedule => schedule.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(schedule => schedule.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(schedule => schedule.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasMany(schedule => schedule.Days)
            .WithOne()
            .HasForeignKey(day => day.WorkScheduleId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_work_schedule_days__work_schedule");

        builder.Navigation(schedule => schedule.Days)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(schedule => schedule.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_work_schedules__public_id");

        // Filtered unique on the active codes only (a code can be reused after inactivation) — the handlers
        // translate a 23505 on this constraint into a clean 409 conflict.
        builder.HasIndex(schedule => new { schedule.TenantId, schedule.NormalizedCode })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName(PayrollMasterConstraintNames.WorkScheduleCodeUnique);

        builder.HasIndex(schedule => new { schedule.TenantId, schedule.IsActive })
            .HasDatabaseName("ix_work_schedules__tenant_active");
    }
}

internal sealed class WorkScheduleDayConfiguration : IEntityTypeConfiguration<WorkScheduleDay>
{
    public void Configure(EntityTypeBuilder<WorkScheduleDay> builder)
    {
        builder.ToTable("work_schedule_days", table =>
            table.HasCheckConstraint(
                "ck_work_schedule_days__day_of_week",
                "day_of_week >= 0 AND day_of_week <= 6"));

        builder.HasKey(day => day.Id)
            .HasName("pk_work_schedule_days");

        builder.Property(day => day.Id)
            .HasColumnName("id");

        builder.Property(day => day.PublicId)
            .HasColumnName("public_id");

        builder.Property(day => day.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(day => day.WorkScheduleId)
            .HasColumnName("work_schedule_id");

        builder.Property(day => day.DayOfWeek)
            .HasColumnName("day_of_week");

        builder.Property(day => day.StartTime)
            .HasColumnName("start_time");

        builder.Property(day => day.EndTime)
            .HasColumnName("end_time");

        builder.Property(day => day.MealStart)
            .HasColumnName("meal_start");

        builder.Property(day => day.MealEnd)
            .HasColumnName("meal_end");

        builder.Property(day => day.NetHours)
            .HasColumnName("net_hours")
            .HasColumnType("numeric(5,2)");

        builder.Property(day => day.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(day => day.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(day => day.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_work_schedule_days__public_id");

        builder.HasIndex(day => new { day.WorkScheduleId, day.DayOfWeek })
            .IsUnique()
            .HasDatabaseName("uq_work_schedule_days__schedule_day");
    }
}
