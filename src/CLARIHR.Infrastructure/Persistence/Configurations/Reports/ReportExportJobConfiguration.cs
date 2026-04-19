using CLARIHR.Domain.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Reports;

internal sealed class ReportExportJobConfiguration : IEntityTypeConfiguration<ReportExportJob>
{
    public void Configure(EntityTypeBuilder<ReportExportJob> builder)
    {
        builder.ToTable("report_export_jobs");

        builder.HasKey(job => job.Id)
            .HasName("pk_report_export_jobs");

        builder.Property(job => job.Id).HasColumnName("id");
        builder.Property(job => job.PublicId).HasColumnName("public_id");
        builder.Property(job => job.TenantId).HasColumnName("tenant_id");
        builder.Property(job => job.ResourceKey).HasColumnName("resource_key").HasMaxLength(120);
        builder.Property(job => job.Format).HasColumnName("format").HasMaxLength(20);
        builder.Property(job => job.ParametersJson).HasColumnName("parameters_json").HasColumnType("jsonb");
        builder.Property(job => job.RequestedByUserId).HasColumnName("requested_by_user_id").HasMaxLength(80);
        builder.Property(job => job.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(job => job.QueuedUtc).HasColumnName("queued_utc");
        builder.Property(job => job.StartedUtc).HasColumnName("started_utc");
        builder.Property(job => job.CompletedUtc).HasColumnName("completed_utc");
        builder.Property(job => job.ExpiresUtc).HasColumnName("expires_utc");
        builder.Property(job => job.LeaseUntilUtc).HasColumnName("lease_until_utc");
        builder.Property(job => job.WorkerId).HasColumnName("worker_id").HasMaxLength(120);
        builder.Property(job => job.Attempts).HasColumnName("attempts");
        builder.Property(job => job.RowCount).HasColumnName("row_count");
        builder.Property(job => job.ArtifactBlobName).HasColumnName("artifact_blob_name").HasMaxLength(1000);
        builder.Property(job => job.ArtifactFileName).HasColumnName("artifact_file_name").HasMaxLength(240);
        builder.Property(job => job.ArtifactContentType).HasColumnName("artifact_content_type").HasMaxLength(120);
        builder.Property(job => job.ArtifactSizeBytes).HasColumnName("artifact_size_bytes");
        builder.Property(job => job.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(120);
        builder.Property(job => job.LastErrorMessage).HasColumnName("last_error_message").HasMaxLength(1000);
        builder.Property(job => job.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();
        builder.Property(job => job.CreatedUtc).HasColumnName("created_utc");
        builder.Property(job => job.ModifiedUtc).HasColumnName("modified_utc");

        builder.HasIndex(job => job.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_report_export_jobs__public_id");

        builder.HasIndex(job => new { job.TenantId, job.QueuedUtc, job.PublicId })
            .HasDatabaseName("ix_report_export_jobs__tenant_queued_public");

        builder.HasIndex(job => new { job.TenantId, job.Status, job.QueuedUtc })
            .HasDatabaseName("ix_report_export_jobs__tenant_status_queued");

        builder.HasIndex(job => new { job.Status, job.LeaseUntilUtc, job.QueuedUtc })
            .HasDatabaseName("ix_report_export_jobs__worker_claim");

        builder.HasIndex(job => new { job.Status, job.ExpiresUtc })
            .HasDatabaseName("ix_report_export_jobs__expiration");
    }
}
