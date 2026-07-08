using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileVacationPeriodConfiguration : IEntityTypeConfiguration<PersonnelFileVacationPeriod>
{
    public void Configure(EntityTypeBuilder<PersonnelFileVacationPeriod> builder)
    {
        builder.ToTable("personnel_file_vacation_periods", table =>
        {
            table.HasCheckConstraint(
                "ck_personnel_file_vacation_periods__dates",
                "period_end_date >= period_start_date");
            table.HasCheckConstraint(
                "ck_personnel_file_vacation_periods__legal_days",
                "legal_days_granted > 0");
            table.HasCheckConstraint(
                "ck_personnel_file_vacation_periods__benefit_days",
                "benefit_days_granted >= 0");
        });

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_vacation_periods");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.PeriodYear).HasColumnName("period_year");
        builder.Property(item => item.PeriodStartDate).HasColumnName("period_start_date");
        builder.Property(item => item.PeriodEndDate).HasColumnName("period_end_date");
        builder.Property(item => item.LegalDaysGranted).HasColumnName("legal_days_granted");
        builder.Property(item => item.BenefitDaysGranted).HasColumnName("benefit_days_granted");
        builder.Property(item => item.GeneratesEnjoymentDays).HasColumnName("generates_enjoyment_days");
        builder.Property(item => item.UsedAnniversary).HasColumnName("used_anniversary");
        builder.Property(item => item.SourceCode).HasColumnName("source_code").HasMaxLength(40).IsRequired();
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_vacation_periods__personnel_file");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_vacation_periods__public_id");

        // At most ONE active period per employee-year (RN-19): the handler duplicate check is the primary
        // guard, this filtered-unique index closes the concurrency race (precedent: retirement open-request).
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.PeriodYear })
            .IsUnique()
            .HasFilter("is_active")
            .HasDatabaseName("uq_pf_vacation_periods__tenant_file_year_active");
    }
}

internal sealed class PersonnelFileVacationRequestConfiguration : IEntityTypeConfiguration<PersonnelFileVacationRequest>
{
    public void Configure(EntityTypeBuilder<PersonnelFileVacationRequest> builder)
    {
        builder.ToTable("personnel_file_vacation_requests", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_vacation_requests__dates",
                "end_date >= start_date"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_vacation_requests");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id").HasMaxLength(100).IsRequired();
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.RequestedDays).HasColumnName("requested_days");
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.PlanLinePublicId).HasColumnName("plan_line_public_id");
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id").HasMaxLength(100);
        builder.Property(item => item.DecisionDateUtc).HasColumnName("decision_date_utc");
        builder.Property(item => item.DecisionNotes).HasColumnName("decision_notes").HasMaxLength(1000);
        builder.Property(item => item.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_vacation_requests__personnel_file");

        builder.HasMany(item => item.Allocations)
            .WithOne()
            .HasForeignKey(allocation => allocation.VacationRequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vacation_request_allocations__request");

        builder.HasMany(item => item.Returns)
            .WithOne()
            .HasForeignKey(entry => entry.VacationRequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_vacation_returns__request");

        builder.Navigation(item => item.Allocations).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(item => item.Returns).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_vacation_requests__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_personnel_file_vacation_requests__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.StartDate })
            .HasDatabaseName("ix_personnel_file_vacation_requests__tenant_start");
    }
}

internal sealed class VacationRequestAllocationConfiguration : IEntityTypeConfiguration<VacationRequestAllocation>
{
    public void Configure(EntityTypeBuilder<VacationRequestAllocation> builder)
    {
        builder.ToTable("vacation_request_allocations", table =>
            table.HasCheckConstraint("ck_vacation_request_allocations__days", "days > 0"));

        builder.HasKey(item => item.Id).HasName("pk_vacation_request_allocations");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.VacationRequestId).HasColumnName("vacation_request_id");
        builder.Property(item => item.VacationPeriodId).HasColumnName("vacation_period_id");
        builder.Property(item => item.Days).HasColumnName("days");
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        // Restrict delete of a referenced period keeps a consumed period from being removed (RF-016).
        builder.HasOne(item => item.VacationPeriod)
            .WithMany()
            .HasForeignKey(item => item.VacationPeriodId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_vacation_request_allocations__vacation_period");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_vacation_request_allocations__public_id");
        builder.HasIndex(item => item.VacationPeriodId)
            .HasDatabaseName("ix_vacation_request_allocations__vacation_period");
        builder.HasIndex(item => new { item.TenantId, item.VacationRequestId })
            .HasDatabaseName("ix_vacation_request_allocations__tenant_request");
    }
}

internal sealed class VacationReturnConfiguration : IEntityTypeConfiguration<VacationReturn>
{
    public void Configure(EntityTypeBuilder<VacationReturn> builder)
    {
        builder.ToTable("vacation_returns", table =>
            table.HasCheckConstraint("ck_vacation_returns__days", "days > 0"));

        builder.HasKey(item => item.Id).HasName("pk_vacation_returns");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.VacationRequestId).HasColumnName("vacation_request_id");
        builder.Property(item => item.Days).HasColumnName("days");
        builder.Property(item => item.ReturnDateUtc).HasColumnName("return_date_utc");
        builder.Property(item => item.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(item => item.DecidedByUserId).HasColumnName("decided_by_user_id").HasMaxLength(100).IsRequired();
        builder.Property(item => item.DistributionJson).HasColumnName("distribution_json").HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_vacation_returns__public_id");
        builder.HasIndex(item => new { item.TenantId, item.VacationRequestId })
            .HasDatabaseName("ix_vacation_returns__tenant_request");
    }
}
