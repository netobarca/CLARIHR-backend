using CLARIHR.Domain.Files;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Files;

internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("files");

        builder.HasKey(f => f.Id).HasName("pk_files");

        builder.Property(f => f.Id).HasColumnName("id");
        builder.Property(f => f.PublicId).HasColumnName("public_id");
        builder.Property(f => f.TenantId).HasColumnName("tenant_id");
        builder.Property(f => f.FileName).HasColumnName("file_name").HasMaxLength(500);
        builder.Property(f => f.ContentType).HasColumnName("content_type").HasMaxLength(200);
        builder.Property(f => f.SizeBytes).HasColumnName("size_bytes");
        builder.Property(f => f.Extension).HasColumnName("extension").HasMaxLength(20);
        builder.Property(f => f.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.ContainerName).HasColumnName("container_name").HasMaxLength(200);
        builder.Property(f => f.ObjectKey).HasColumnName("object_key").HasMaxLength(1000);
        builder.Property(f => f.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.Purpose).HasColumnName("purpose").HasConversion<string>().HasMaxLength(50);
        builder.Property(f => f.UploadType).HasColumnName("upload_type").HasConversion<string>().HasMaxLength(30);
        builder.Property(f => f.EntityId).HasColumnName("entity_id");
        builder.Property(f => f.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(80);
        builder.Property(f => f.UploadConfirmedUtc).HasColumnName("upload_confirmed_utc");
        builder.Property(f => f.DeletedUtc).HasColumnName("deleted_utc");
        builder.Property(f => f.FailureReason).HasColumnName("failure_reason").HasMaxLength(1000);
        builder.Property(f => f.CreatedUtc).HasColumnName("created_utc");
        builder.Property(f => f.ModifiedUtc).HasColumnName("modified_utc");
        builder.Property(f => f.ConcurrencyToken).HasColumnName("concurrency_token").IsConcurrencyToken();

        // Unique index on provider + object_key (no duplicate blobs)
        builder.HasIndex(f => new { f.Provider, f.ObjectKey })
            .IsUnique()
            .HasDatabaseName("ux_files__provider_object_key");

        // Query by owner + purpose (find user's profile images)
        builder.HasIndex(f => new { f.CreatedByUserId, f.Purpose })
            .HasDatabaseName("ix_files__owner_purpose");

        // Cleanup job: find pending uploads older than threshold
        builder.HasIndex(f => new { f.Status, f.CreatedUtc })
            .HasDatabaseName("ix_files__status_created_at");

        // Tenant-scoped lookup by public_id
        builder.HasIndex(f => new { f.TenantId, f.PublicId })
            .IsUnique()
            .HasDatabaseName("ix_files__tenant_public");
    }
}
