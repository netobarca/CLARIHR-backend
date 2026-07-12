using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileNotWorkedTimeConfiguration
    : IEntityTypeConfiguration<PersonnelFileNotWorkedTime>
{
    public void Configure(EntityTypeBuilder<PersonnelFileNotWorkedTime> builder)
    {
        builder.ToTable("pf_not_worked_times", table =>
        {
            table.HasCheckConstraint("ck_pf_not_worked_times__range", "end_date >= start_date");
            table.HasCheckConstraint(
                "ck_pf_not_worked_times__discount_percent",
                "discount_percent_snapshot >= 0 and discount_percent_snapshot <= 100");
        });

        builder.HasKey(item => item.Id).HasName("pk_pf_not_worked_times");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");

        builder.Property(item => item.TypeCodeSnapshot)
            .HasColumnName("type_code_snapshot").HasMaxLength(PersonnelFileNotWorkedTime.MaxTypeCodeLength);
        builder.Property(item => item.TypeNameSnapshot)
            .HasColumnName("type_name_snapshot").HasMaxLength(PersonnelFileNotWorkedTime.MaxTypeNameLength);
        builder.Property(item => item.UsesWorkSchedule).HasColumnName("uses_work_schedule");
        builder.Property(item => item.CountsHoliday).HasColumnName("counts_holiday");
        builder.Property(item => item.CountsSaturday).HasColumnName("counts_saturday");
        builder.Property(item => item.CountsRestDay).HasColumnName("counts_rest_day");
        builder.Property(item => item.CountsSeventhDayPenalty).HasColumnName("counts_seventh_day_penalty");
        builder.Property(item => item.DiscountPercentSnapshot)
            .HasColumnName("discount_percent_snapshot").HasColumnType("numeric(6,2)");
        builder.Property(item => item.DeductionConceptTypeCodeSnapshot)
            .HasColumnName("deduction_concept_code").HasMaxLength(PersonnelFileNotWorkedTime.MaxConceptCodeLength);
        builder.Property(item => item.IncomeConceptTypeCodeSnapshot)
            .HasColumnName("income_concept_code").HasMaxLength(PersonnelFileNotWorkedTime.MaxConceptCodeLength);

        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.Hours).HasColumnName("hours").HasColumnType("numeric(9,2)");
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(PersonnelFileNotWorkedTime.MaxReasonLength);
        builder.Property(item => item.OriginCode)
            .HasColumnName("origin_code").HasMaxLength(PersonnelFileNotWorkedTime.MaxOriginCodeLength);

        builder.Property(item => item.CalendarDays).HasColumnName("calendar_days");
        builder.Property(item => item.ComputableDays).HasColumnName("computable_days");
        builder.Property(item => item.SeventhDayPenaltyDays).HasColumnName("seventh_day_penalty_days");
        builder.Property(item => item.DiscountedDays).HasColumnName("discounted_days").HasColumnType("numeric(9,2)");
        builder.Property(item => item.DailySalarySnapshot).HasColumnName("daily_salary_snapshot").HasColumnType("numeric(18,2)");
        builder.Property(item => item.DiscountAmount).HasColumnName("discount_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.CurrencyCode).HasColumnName("currency_code").HasMaxLength(3);
        builder.Property(item => item.DetailJson).HasColumnName("detail_json").HasColumnType("jsonb");

        builder.Property(item => item.StatusCode)
            .HasColumnName("status_code").HasMaxLength(PersonnelFileNotWorkedTime.MaxStatusCodeLength);
        builder.Property(item => item.RegisteredByUserId).HasColumnName("registered_by_user_id");
        builder.Property(item => item.RegisteredUtc).HasColumnName("registered_utc");
        builder.Property(item => item.AnnulledByUserId).HasColumnName("annulled_by_user_id");
        builder.Property(item => item.AnnulledUtc).HasColumnName("annulled_utc");
        builder.Property(item => item.AnnulmentReason)
            .HasColumnName("annulment_reason").HasMaxLength(PersonnelFileNotWorkedTime.MaxAnnulmentReasonLength);
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_pf_not_worked_times__personnel_file");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_pf_not_worked_times__public_id");

        builder.HasIndex(item => new { item.TenantId, item.StartDate })
            .HasDatabaseName("ix_pf_not_worked_times__tenant_start");

        builder.HasIndex(item => new { item.PersonnelFileId, item.StartDate })
            .HasDatabaseName("ix_pf_not_worked_times__file_start");
    }
}
