using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class ExitInterviewSubmissionConfiguration : IEntityTypeConfiguration<ExitInterviewSubmission>
{
    public void Configure(EntityTypeBuilder<ExitInterviewSubmission> builder)
    {
        builder.ToTable("exit_interview_submissions");
        builder.HasKey(item => item.Id).HasName("pk_exit_interview_submissions");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.ExitInterviewFormId).HasColumnName("exit_interview_form_id");
        builder.Property(item => item.FormVersion).HasColumnName("form_version");
        builder.Property(item => item.IsAnonymous).HasColumnName("is_anonymous");
        builder.Property(item => item.PersonnelFileId).HasColumnName("personnel_file_id");
        builder.Property(item => item.SubmittedByUserId).HasColumnName("submitted_by_user_id");
        builder.Property(item => item.RetirementReasonCode).HasColumnName("retirement_reason_code").HasMaxLength(80);
        builder.Property(item => item.RetirementCategoryCode).HasColumnName("retirement_category_code").HasMaxLength(80);
        builder.Property(item => item.SeparationType).HasColumnName("separation_type").HasMaxLength(20);
        builder.Property(item => item.PositionSlotPublicId).HasColumnName("position_slot_public_id");
        builder.Property(item => item.PlazaSnapshot).HasColumnName("plaza_snapshot").HasMaxLength(200);
        builder.Property(item => item.Period).HasColumnName("period").HasMaxLength(7);
        builder.Property(item => item.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(item => item.SubmittedUtc).HasColumnName("submitted_utc");
        builder.Property(item => item.TotalScore).HasColumnName("total_score").HasColumnType("numeric(6,2)");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.ExitInterviewForm)
            .WithMany()
            .HasForeignKey(item => item.ExitInterviewFormId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_exit_interview_submissions__form");

        builder.HasOne(item => item.PersonnelFile)
            .WithMany()
            .HasForeignKey(item => item.PersonnelFileId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_exit_interview_submissions__personnel_file");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_exit_interview_submissions__public_id");
        builder.HasIndex(item => new { item.TenantId, item.PersonnelFileId, item.Status })
            .HasDatabaseName("ix_exit_interview_submissions__file_status");
        builder.HasIndex(item => new { item.TenantId, item.Period, item.RetirementCategoryCode })
            .HasDatabaseName("ix_exit_interview_submissions__period_category");
        builder.HasIndex(item => new { item.TenantId, item.ExitInterviewFormId })
            .HasDatabaseName("ix_exit_interview_submissions__form");
    }
}

internal sealed class ExitInterviewAnswerConfiguration : IEntityTypeConfiguration<ExitInterviewAnswer>
{
    public void Configure(EntityTypeBuilder<ExitInterviewAnswer> builder)
    {
        builder.ToTable("exit_interview_answers");
        builder.HasKey(item => item.Id).HasName("pk_exit_interview_answers");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.ExitInterviewSubmissionId).HasColumnName("exit_interview_submission_id");
        builder.Property(item => item.FieldKeySnapshot).HasColumnName("field_key_snapshot").HasMaxLength(100);
        builder.Property(item => item.TitleSnapshot).HasColumnName("title_snapshot").HasMaxLength(300);
        builder.Property(item => item.ControlTypeCode).HasColumnName("control_type_code").HasMaxLength(40);
        builder.Property(item => item.ValueText).HasColumnName("value_text").HasMaxLength(4000);
        builder.Property(item => item.ValueNumber).HasColumnName("value_number").HasColumnType("numeric(18,4)");
        builder.Property(item => item.ValueDate).HasColumnName("value_date");
        builder.Property(item => item.ValueBool).HasColumnName("value_bool");
        builder.Property(item => item.SelectedOptionCodes).HasColumnName("selected_option_codes").HasMaxLength(2000);
        builder.Property(item => item.WeightSnapshot).HasColumnName("weight_snapshot").HasColumnType("numeric(9,2)");
        builder.Property(item => item.NormalizedScore).HasColumnName("normalized_score").HasColumnType("numeric(6,2)");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.ExitInterviewSubmission)
            .WithMany()
            .HasForeignKey(item => item.ExitInterviewSubmissionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_exit_interview_answers__submission");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_exit_interview_answers__public_id");
        builder.HasIndex(item => new { item.TenantId, item.ExitInterviewSubmissionId })
            .HasDatabaseName("ix_exit_interview_answers__submission");
    }
}
