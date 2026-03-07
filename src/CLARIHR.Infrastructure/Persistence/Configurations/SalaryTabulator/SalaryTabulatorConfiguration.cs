using CLARIHR.Domain.SalaryTabulator;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.SalaryTabulator;

internal sealed class SalaryTabulatorLineConfiguration : IEntityTypeConfiguration<SalaryTabulatorLine>
{
    public void Configure(EntityTypeBuilder<SalaryTabulatorLine> builder)
    {
        builder.ToTable("salary_tabulator_lines");

        builder.HasKey(line => line.Id)
            .HasName("pk_salary_tabulator_lines");

        builder.Property(line => line.Id).HasColumnName("id");
        builder.Property(line => line.PublicId).HasColumnName("public_id");
        builder.Property(line => line.TenantId).HasColumnName("tenant_id");

        builder.Property(line => line.SalaryClassCode)
            .HasColumnName("salary_class_code")
            .HasMaxLength(50);

        builder.Property(line => line.NormalizedSalaryClassCode)
            .HasColumnName("normalized_salary_class_code")
            .HasMaxLength(50);

        builder.Property(line => line.SalaryScaleCode)
            .HasColumnName("salary_scale_code")
            .HasMaxLength(50);

        builder.Property(line => line.NormalizedSalaryScaleCode)
            .HasColumnName("normalized_salary_scale_code")
            .HasMaxLength(50);

        builder.Property(line => line.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3);

        builder.Property(line => line.BaseAmount)
            .HasColumnName("base_amount")
            .HasPrecision(18, 2);

        builder.Property(line => line.MinAmount)
            .HasColumnName("min_amount")
            .HasPrecision(18, 2);

        builder.Property(line => line.MaxAmount)
            .HasColumnName("max_amount")
            .HasPrecision(18, 2);

        builder.Property(line => line.EffectiveFromUtc).HasColumnName("effective_from_utc");
        builder.Property(line => line.EffectiveToUtc).HasColumnName("effective_to_utc");
        builder.Property(line => line.IsActive).HasColumnName("is_active");
        builder.Property(line => line.Version).HasColumnName("version");

        builder.Property(line => line.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.Property(line => line.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(line => line.CreatedUtc).HasColumnName("created_utc");
        builder.Property(line => line.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(line => line.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_salary_tabulator_lines__public_id");

        builder.HasIndex(line => new
            {
                line.TenantId,
                line.NormalizedSalaryClassCode,
                line.NormalizedSalaryScaleCode,
                line.EffectiveFromUtc
            })
            .IsUnique()
            .HasDatabaseName("uq_salary_tabulator_lines__tenant_class_scale_effective_from");

        builder.HasIndex(line => new
            {
                line.TenantId,
                line.NormalizedSalaryClassCode,
                line.NormalizedSalaryScaleCode,
                line.IsActive
            })
            .HasDatabaseName("ix_salary_tabulator_lines__tenant_class_scale_active");
    }
}

internal sealed class SalaryTabulatorChangeRequestConfiguration : IEntityTypeConfiguration<SalaryTabulatorChangeRequest>
{
    public void Configure(EntityTypeBuilder<SalaryTabulatorChangeRequest> builder)
    {
        builder.ToTable("salary_tabulator_change_requests");

        builder.HasKey(request => request.Id)
            .HasName("pk_salary_tabulator_change_requests");

        builder.Property(request => request.Id).HasColumnName("id");
        builder.Property(request => request.PublicId).HasColumnName("public_id");
        builder.Property(request => request.TenantId).HasColumnName("tenant_id");

        builder.Property(request => request.RequestNumber)
            .HasColumnName("request_number")
            .HasMaxLength(40);

        builder.Property(request => request.Reason)
            .HasColumnName("reason")
            .HasMaxLength(1000);

        builder.Property(request => request.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(request => request.EffectiveFromUtc).HasColumnName("effective_from_utc");
        builder.Property(request => request.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(request => request.SubmittedAtUtc).HasColumnName("submitted_at_utc");
        builder.Property(request => request.DecidedByUserId).HasColumnName("decided_by_user_id");
        builder.Property(request => request.DecidedAtUtc).HasColumnName("decided_at_utc");

        builder.Property(request => request.DecisionComment)
            .HasColumnName("decision_comment")
            .HasMaxLength(1000);

        builder.Property(request => request.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(request => request.CreatedUtc).HasColumnName("created_utc");
        builder.Property(request => request.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(request => request.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_salary_tabulator_change_requests__public_id");

        builder.HasIndex(request => new { request.TenantId, request.RequestNumber })
            .IsUnique()
            .HasDatabaseName("ix_salary_tabulator_requests__tenant_request_number");

        builder.HasIndex(request => new { request.TenantId, request.Status, request.CreatedUtc })
            .HasDatabaseName("ix_salary_tabulator_requests__tenant_status_created");

        builder.HasMany(request => request.Items)
            .WithOne(item => item.SalaryTabulatorChangeRequest)
            .HasForeignKey(item => item.SalaryTabulatorChangeRequestId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_salary_tabulator_change_request_items__request");

        builder.Navigation(request => request.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SalaryTabulatorChangeRequestItemConfiguration : IEntityTypeConfiguration<SalaryTabulatorChangeRequestItem>
{
    public void Configure(EntityTypeBuilder<SalaryTabulatorChangeRequestItem> builder)
    {
        builder.ToTable("salary_tabulator_change_request_items");

        builder.HasKey(item => item.Id)
            .HasName("pk_salary_tabulator_change_request_items");

        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.TenantId).HasColumnName("tenant_id");
        builder.Property(item => item.SalaryTabulatorChangeRequestId).HasColumnName("salary_tabulator_change_request_id");

        builder.Property(item => item.SalaryClassCode)
            .HasColumnName("salary_class_code")
            .HasMaxLength(50);

        builder.Property(item => item.NormalizedSalaryClassCode)
            .HasColumnName("normalized_salary_class_code")
            .HasMaxLength(50);

        builder.Property(item => item.SalaryScaleCode)
            .HasColumnName("salary_scale_code")
            .HasMaxLength(50);

        builder.Property(item => item.NormalizedSalaryScaleCode)
            .HasColumnName("normalized_salary_scale_code")
            .HasMaxLength(50);

        builder.Property(item => item.CurrencyCode)
            .HasColumnName("currency_code")
            .HasMaxLength(3);

        builder.Property(item => item.ChangeType)
            .HasColumnName("change_type")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(item => item.CurrentBaseAmount)
            .HasColumnName("current_base_amount")
            .HasPrecision(18, 2);

        builder.Property(item => item.ProposedBaseAmount)
            .HasColumnName("proposed_base_amount")
            .HasPrecision(18, 2);

        builder.Property(item => item.CurrentMinAmount)
            .HasColumnName("current_min_amount")
            .HasPrecision(18, 2);

        builder.Property(item => item.ProposedMinAmount)
            .HasColumnName("proposed_min_amount")
            .HasPrecision(18, 2);

        builder.Property(item => item.CurrentMaxAmount)
            .HasColumnName("current_max_amount")
            .HasPrecision(18, 2);

        builder.Property(item => item.ProposedMaxAmount)
            .HasColumnName("proposed_max_amount")
            .HasPrecision(18, 2);

        builder.Property(item => item.Notes)
            .HasColumnName("notes")
            .HasMaxLength(1000);

        builder.Property(item => item.CreatedUtc).HasColumnName("created_utc");
        builder.Property(item => item.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(item => item.SalaryTabulatorChangeRequestId)
            .HasDatabaseName("ix_salary_tabulator_items__request");
    }
}
