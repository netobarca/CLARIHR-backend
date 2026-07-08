using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class PersonnelFileIncapacityConfiguration : IEntityTypeConfiguration<PersonnelFileIncapacity>
{
    public void Configure(EntityTypeBuilder<PersonnelFileIncapacity> builder)
    {
        builder.ToTable("personnel_file_incapacities", table =>
            table.HasCheckConstraint(
                "ck_personnel_file_incapacities__dates",
                "end_date is null or end_date >= start_date"));

        builder.HasKey(item => item.Id).HasName("pk_personnel_file_incapacities");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.RequesterFilePublicId).HasColumnName("requester_file_public_id");
        builder.Property(item => item.RequesterNameSnapshot).HasColumnName("requester_name_snapshot").HasMaxLength(200);
        builder.Property(item => item.RequestedByUserId).HasColumnName("requested_by_user_id").HasMaxLength(100).IsRequired();
        builder.Property(item => item.OriginCode).HasColumnName("origin_code").HasMaxLength(40).IsRequired();
        builder.Property(item => item.IncapacityRiskId).HasColumnName("incapacity_risk_id");
        builder.Property(item => item.RiskCodeSnapshot).HasColumnName("risk_code_snapshot").HasMaxLength(50).IsRequired();
        builder.Property(item => item.RiskCountsSeventhDaySnapshot).HasColumnName("risk_counts_seventh_day_snapshot");
        builder.Property(item => item.RiskCountsSaturdaySnapshot).HasColumnName("risk_counts_saturday_snapshot");
        builder.Property(item => item.RiskCountsHolidaySnapshot).HasColumnName("risk_counts_holiday_snapshot");
        builder.Property(item => item.RiskUsesFundSnapshot).HasColumnName("risk_uses_fund_snapshot");
        builder.Property(item => item.RiskHasSubsidySnapshot).HasColumnName("risk_has_subsidy_snapshot");
        builder.Property(item => item.MedicalClinicId).HasColumnName("medical_clinic_id");
        builder.Property(item => item.IncapacityTypeId).HasColumnName("incapacity_type_id");
        builder.Property(item => item.AssignedPositionPublicId).HasColumnName("assigned_position_public_id");
        builder.Property(item => item.PayrollTypeCode).HasColumnName("payroll_type_code").HasMaxLength(80);
        builder.Property(item => item.PayrollPeriodDefinitionId).HasColumnName("payroll_period_definition_id");
        builder.Property(item => item.StartDate).HasColumnName("start_date");
        builder.Property(item => item.EndDate).HasColumnName("end_date");
        builder.Property(item => item.CalendarDays).HasColumnName("calendar_days");
        builder.Property(item => item.ComputableDays).HasColumnName("computable_days");
        builder.Property(item => item.ComputableDaysOverridden).HasColumnName("computable_days_overridden");
        builder.Property(item => item.OverrideNote).HasColumnName("override_note").HasMaxLength(500);
        builder.Property(item => item.SubsidizedDays).HasColumnName("subsidized_days");
        builder.Property(item => item.DiscountDays).HasColumnName("discount_days");
        builder.Property(item => item.EmployerDays).HasColumnName("employer_days");
        builder.Property(item => item.MonthlyBaseSalary).HasColumnName("monthly_base_salary").HasColumnType("numeric(18,2)");
        builder.Property(item => item.DailySalary).HasColumnName("daily_salary").HasColumnType("numeric(18,2)");
        builder.Property(item => item.SubsidyAmount).HasColumnName("subsidy_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.DiscountAmount).HasColumnName("discount_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.EmployerAmount).HasColumnName("employer_amount").HasColumnType("numeric(18,2)");
        builder.Property(item => item.TrancheDetailJson).HasColumnName("tranche_detail_json").HasColumnType("jsonb");
        builder.Property(item => item.StatusCode).HasColumnName("status_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.ExtendsIncapacityId).HasColumnName("extends_incapacity_id");
        builder.Property(item => item.ConfirmedByUserId).HasColumnName("confirmed_by_user_id").HasMaxLength(100);
        builder.Property(item => item.ConfirmedAtUtc).HasColumnName("confirmed_at_utc");
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
            .HasConstraintName("fk_personnel_file_incapacities__personnel_file");

        // Hard FK to the risk master (the flag snapshot preserves history; restrict keeps referenced
        // masters from being deleted while in use — same for type/clinic/payroll period below).
        builder.HasOne(item => item.IncapacityRisk)
            .WithMany()
            .HasForeignKey(item => item.IncapacityRiskId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_incapacities__incapacity_risk");

        builder.HasOne(item => item.IncapacityType)
            .WithMany()
            .HasForeignKey(item => item.IncapacityTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_incapacities__incapacity_type");

        builder.HasOne(item => item.MedicalClinic)
            .WithMany()
            .HasForeignKey(item => item.MedicalClinicId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_incapacities__medical_clinic");

        builder.HasOne(item => item.PayrollPeriodDefinition)
            .WithMany()
            .HasForeignKey(item => item.PayrollPeriodDefinitionId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_incapacities__payroll_period_definition");

        // Extension chain (RN-03): self-referencing FK to the incapacity this record extends.
        builder.HasOne(item => item.ExtendsIncapacity)
            .WithMany()
            .HasForeignKey(item => item.ExtendsIncapacityId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_incapacities__extends_incapacity");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_incapacities__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.StatusCode })
            .HasDatabaseName("ix_personnel_file_incapacities__tenant_file_status");
        builder.HasIndex(item => new { item.TenantId, item.StartDate })
            .HasDatabaseName("ix_personnel_file_incapacities__tenant_start");
    }
}

internal sealed class PersonnelFileIncapacityDocumentConfiguration : IEntityTypeConfiguration<PersonnelFileIncapacityDocument>
{
    public void Configure(EntityTypeBuilder<PersonnelFileIncapacityDocument> builder)
    {
        builder.ToTable("personnel_file_incapacity_documents");
        builder.HasKey(item => item.Id).HasName("pk_personnel_file_incapacity_documents");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PersonnelFileIncapacityId).HasColumnName("personnel_file_incapacity_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.DocumentTypeCatalogItemId).HasColumnName("document_type_catalog_item_id");
        builder.Property(item => item.FilePublicId).HasColumnName("file_public_id");
        builder.Property(item => item.Observations).HasColumnName("observations").HasMaxLength(2000);
        builder.Property(item => item.FileName).HasColumnName("file_name").HasMaxLength(260);
        builder.Property(item => item.ContentType).HasColumnName("content_type").HasMaxLength(200);
        builder.Property(item => item.SizeBytes).HasColumnName("size_bytes");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.Incapacity)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileIncapacityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_personnel_file_incapacity_documents__incapacity");

        // Document-type classification is OPTIONAL (D-22 — "constancia de cualquier índole"), so the FK is
        // nullable; restrict delete keeps the catalog item from being removed while referenced.
        builder.HasOne(item => item.DocumentTypeCatalogItem)
            .WithMany()
            .HasForeignKey(item => item.DocumentTypeCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_personnel_file_incapacity_documents__document_type");

        builder.HasIndex(item => item.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_personnel_file_incapacity_documents__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileIncapacityId, item.IsActive })
            .HasDatabaseName("ix_personnel_file_incapacity_documents__tenant_incapacity_active");
        builder.HasIndex(item => item.DocumentTypeCatalogItemId)
            .HasDatabaseName("ix_personnel_file_incapacity_documents__document_type");
        builder.HasIndex(item => item.FilePublicId)
            .HasDatabaseName("ix_personnel_file_incapacity_documents__file_public_id");
    }
}
