using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileLactationPeriodConfiguration : IEntityTypeConfiguration<PersonnelFileLactationPeriod>
{
    public void Configure(EntityTypeBuilder<PersonnelFileLactationPeriod> builder)
    {
        builder.ToTable("personnel_file_lactation_periods", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_lactation_periods__dates",
                "end_date >= start_date"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_lactation_periods");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id").HasMaxLength(100).IsRequired();
        builder.Property(item => item.IncapacityTypeId).HasColumnName("incapacity_type_id");
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.AnnulmentReason).HasColumnName("annulment_reason").HasMaxLength(500);
        builder.Property(item => item.AnnulledAtUtc).HasColumnName("annulled_at_utc");
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_lactation_periods__personnel_file");

        builder.HasOne(item => item.IncapacityType)
            .WithMany()
            .HasForeignKey(item => item.IncapacityTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_lactation_periods__incapacity_type");

        builder.HasMany(item => item.Schedules)
            .WithOne()
            .HasForeignKey(schedule => schedule.LactationPeriodId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_lactation_schedules__lactation_period");

        builder.Navigation(item => item.Schedules)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_lactation_periods__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_personnel_file_lactation_periods__tenant_file_status");
    }
}

internal sealed class LactationScheduleConfiguration : IEntityTypeConfiguration<LactationSchedule>
{
    public void Configure(EntityTypeBuilder<LactationSchedule> builder)
    {
        builder.ToTable("lactation_schedules", table =>
        {
            table.HasCheckConstraint(
                "ck_lactation_schedules__dates",
                "end_date >= start_date");
            table.HasCheckConstraint(
                "ck_lactation_schedules__daily_permits_count",
                "daily_permits_count >= 1");
            table.HasCheckConstraint(
                "ck_lactation_schedules__minutes_per_permit",
                "minutes_per_permit >= 1");
        });

        builder.HasKey(item => item.Id).HasName("pk_lactation_schedules");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.LactationPeriodId).HasColumnName("lactation_period_id");
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.DailyPermitsCount).HasColumnName("daily_permits_count");
        builder.Property(item => item.MinutesPerPermit).HasColumnName("minutes_per_permit");
        builder.Property(item => item.SortOrder).HasColumnName("sort_order");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_lactation_schedules__public_id");
        builder.HasIndex(item => new { item.TenantId, item.LactationPeriodId, item.SortOrder })
            .HasDatabaseName("ix_lactation_schedules__period_sort");
    }
}
