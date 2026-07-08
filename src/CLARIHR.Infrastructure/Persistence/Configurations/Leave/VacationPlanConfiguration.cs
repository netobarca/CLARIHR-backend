using CLARIHR.Domain.Leave;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Leave;

internal sealed class VacationPlanConfiguration : IEntityTypeConfiguration<VacationPlan>
{
    public void Configure(EntityTypeBuilder<VacationPlan> builder)
    {
        builder.ToTable("vacation_plans");

        builder.HasKey(item => item.Id).HasName("pk_vacation_plans");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.PlanYear).HasColumnName("plan_year");
        builder.Property(item => item.RequestDate).HasColumnName("request_date");
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id").HasMaxLength(100).IsRequired();
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(40).IsRequired();
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasMany(item => item.Lines)
            .WithOne()
            .HasForeignKey(line => line.VacationPlanId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vacation_plan_lines__vacation_plan");

        builder.Navigation(item => item.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_vacation_plans__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PlanYear, item.StatusCode })
            .HasDatabaseName("ix_vacation_plans__tenant_year_status");
    }
}

internal sealed class VacationPlanLineConfiguration : IEntityTypeConfiguration<VacationPlanLine>
{
    public void Configure(EntityTypeBuilder<VacationPlanLine> builder)
    {
        builder.ToTable("vacation_plan_lines", table =>
        {
            table.HasCheckConstraint("ck_vacation_plan_lines__dates", "end_date >= start_date");
            table.HasCheckConstraint("ck_vacation_plan_lines__days", "days > 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_vacation_plan_lines");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.VacationPlanId).HasColumnName("vacation_plan_id");
        builder.Property(item => item.PersonnelFilePublicId).HasColumnName("personnel_file_public_id");
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.Days).HasColumnName("days");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_vacation_plan_lines__public_id");
        builder.HasIndex(item => new { item.TenantId, item.VacationPlanId, item.PersonnelFilePublicId })
            .HasDatabaseName("ix_vacation_plan_lines__plan_employee");
    }
}
