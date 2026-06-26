using CLARIHR.Domain.PersonnelFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.PersonnelFiles;

internal sealed class ExitInterviewFormConfiguration : IEntityTypeConfiguration<ExitInterviewForm>
{
    public void Configure(EntityTypeBuilder<ExitInterviewForm> builder)
    {
        builder.ToTable("exit_interview_forms");
        builder.HasKey(item => item.Id).HasName("pk_exit_interview_forms");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.NormalizedName).HasColumnName("normalized_name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(item => item.IsAnonymous).HasColumnName("is_anonymous");
        builder.Property(item => item.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
        builder.Property(item => item.Version).HasColumnName("version");
        builder.Property(item => item.RetirementReasonCode).HasColumnName("retirement_reason_code").HasMaxLength(80);
        builder.Property(item => item.IsActiveForReason).HasColumnName("is_active_for_reason");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_exit_interview_forms__public_id");
        builder.HasIndex(item => new { item.TenantId, item.NormalizedName })
            .IsUnique()
            .HasDatabaseName("uq_exit_interview_forms__tenant_name");
        builder.HasIndex(item => new { item.TenantId, item.Status })
            .HasDatabaseName("ix_exit_interview_forms__tenant_status");

        // Single active published form per reason (D-03): at most one row per (tenant, reason) that is the
        // active, published form for that reason.
        builder.HasIndex(item => new { item.TenantId, item.RetirementReasonCode })
            .IsUnique()
            .HasFilter("is_active_for_reason AND status = 'Published' AND retirement_reason_code IS NOT NULL")
            .HasDatabaseName("uq_exit_interview_forms__reason_active");
    }
}

internal sealed class ExitInterviewFormGroupConfiguration : IEntityTypeConfiguration<ExitInterviewFormGroup>
{
    public void Configure(EntityTypeBuilder<ExitInterviewFormGroup> builder)
    {
        builder.ToTable("exit_interview_form_groups");
        builder.HasKey(item => item.Id).HasName("pk_exit_interview_form_groups");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.ExitInterviewFormId).HasColumnName("exit_interview_form_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(item => item.DisplayOrder).HasColumnName("display_order");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.ExitInterviewForm)
            .WithMany()
            .HasForeignKey(item => item.ExitInterviewFormId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_exit_interview_form_groups__form");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_exit_interview_form_groups__public_id");
        builder.HasIndex(item => new { item.TenantId, item.ExitInterviewFormId, item.DisplayOrder })
            .HasDatabaseName("ix_exit_interview_form_groups__form_order");
    }
}

internal sealed class ExitInterviewFormFieldConfiguration : IEntityTypeConfiguration<ExitInterviewFormField>
{
    public void Configure(EntityTypeBuilder<ExitInterviewFormField> builder)
    {
        builder.ToTable("exit_interview_form_fields");
        builder.HasKey(item => item.Id).HasName("pk_exit_interview_form_fields");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.ExitInterviewFormId).HasColumnName("exit_interview_form_id");
        builder.Property(item => item.ExitInterviewFormGroupId).HasColumnName("exit_interview_form_group_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.ControlTypeCode).HasColumnName("control_type_code").HasMaxLength(40).IsRequired();
        builder.Property(item => item.FieldKey).HasColumnName("field_key").HasMaxLength(100).IsRequired();
        builder.Property(item => item.NormalizedFieldKey).HasColumnName("normalized_field_key").HasMaxLength(100).IsRequired();
        builder.Property(item => item.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(item => item.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(item => item.Weight).HasColumnName("weight").HasColumnType("numeric(9,2)");
        builder.Property(item => item.IsRequired).HasColumnName("is_required");
        builder.Property(item => item.DisplayOrder).HasColumnName("display_order");
        builder.Property(item => item.MinValue).HasColumnName("min_value").HasColumnType("numeric(18,4)");
        builder.Property(item => item.MaxValue).HasColumnName("max_value").HasColumnType("numeric(18,4)");
        builder.Property(item => item.MaxLength).HasColumnName("max_length");
        builder.Property(item => item.ScaleMax).HasColumnName("scale_max");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.ExitInterviewForm)
            .WithMany()
            .HasForeignKey(item => item.ExitInterviewFormId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_exit_interview_form_fields__form");

        builder.HasOne(item => item.ExitInterviewFormGroup)
            .WithMany()
            .HasForeignKey(item => item.ExitInterviewFormGroupId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_exit_interview_form_fields__group");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_exit_interview_form_fields__public_id");
        builder.HasIndex(item => new { item.ExitInterviewFormId, item.NormalizedFieldKey })
            .IsUnique()
            .HasDatabaseName("uq_exit_interview_form_fields__form_key");
        builder.HasIndex(item => new { item.TenantId, item.ExitInterviewFormId, item.DisplayOrder })
            .HasDatabaseName("ix_exit_interview_form_fields__form_order");
    }
}

internal sealed class ExitInterviewFormFieldOptionConfiguration : IEntityTypeConfiguration<ExitInterviewFormFieldOption>
{
    public void Configure(EntityTypeBuilder<ExitInterviewFormFieldOption> builder)
    {
        builder.ToTable("exit_interview_form_field_options");
        builder.HasKey(item => item.Id).HasName("pk_exit_interview_form_field_options");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.ExitInterviewFormFieldId).HasColumnName("exit_interview_form_field_id");
        builder.Property(item => item.PublicId).HasColumnName("public_id");
        builder.Property(item => item.OptionCode).HasColumnName("option_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.NormalizedOptionCode).HasColumnName("normalized_option_code").HasMaxLength(80).IsRequired();
        builder.Property(item => item.Label).HasColumnName("label").HasMaxLength(300).IsRequired();
        builder.Property(item => item.Score).HasColumnName("score").HasColumnType("numeric(9,2)");
        builder.Property(item => item.DisplayOrder).HasColumnName("display_order");
        builder.Property(item => item.IsActive).HasColumnName("is_active");
        builder.Property(item => item.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasOne(item => item.ExitInterviewFormField)
            .WithMany()
            .HasForeignKey(item => item.ExitInterviewFormFieldId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_exit_interview_form_field_options__field");

        builder.HasIndex(item => item.PublicId).IsUnique().HasDatabaseName("uq_exit_interview_form_field_options__public_id");
        builder.HasIndex(item => new { item.ExitInterviewFormFieldId, item.NormalizedOptionCode })
            .IsUnique()
            .HasDatabaseName("uq_exit_interview_form_field_options__field_code");
        builder.HasIndex(item => new { item.TenantId, item.ExitInterviewFormFieldId, item.DisplayOrder })
            .HasDatabaseName("ix_exit_interview_form_field_options__field_order");
    }
}
