using CLARIHR.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Platform;

internal sealed class PlatformAuditLogConfiguration : IEntityTypeConfiguration<PlatformAuditLog>
{
    public void Configure(EntityTypeBuilder<PlatformAuditLog> builder)
    {
        builder.ToTable("platform_audit_logs");

        builder.HasKey(log => log.Id)
            .HasName("pk_platform_audit_logs");

        builder.Property(log => log.Id)
            .HasColumnName("id");

        builder.Property(log => log.PublicId)
            .HasColumnName("public_id");

        builder.Property(log => log.ActorUserId)
            .HasColumnName("actor_user_id");

        builder.Property(log => log.ActorEmail)
            .HasColumnName("actor_email")
            .HasMaxLength(320);

        builder.Property(log => log.EventType)
            .HasColumnName("event_type")
            .HasMaxLength(100);

        builder.Property(log => log.EntityType)
            .HasColumnName("entity_type")
            .HasMaxLength(50);

        builder.Property(log => log.EntityId)
            .HasColumnName("entity_id");

        builder.Property(log => log.EntityKey)
            .HasColumnName("entity_key")
            .HasMaxLength(150);

        builder.Property(log => log.Action)
            .HasColumnName("action")
            .HasMaxLength(50);

        builder.Property(log => log.Summary)
            .HasColumnName("summary")
            .HasMaxLength(500);

        builder.Property(log => log.BeforeJson)
            .HasColumnName("before_json")
            .HasColumnType("jsonb");

        builder.Property(log => log.AfterJson)
            .HasColumnName("after_json")
            .HasColumnType("jsonb");

        builder.Property(log => log.DiffJson)
            .HasColumnName("diff_json")
            .HasColumnType("jsonb");

        builder.Property(log => log.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(45);

        builder.Property(log => log.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(1000);

        builder.Property(log => log.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(log => log.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(log => log.PublicId)
            .IsUnique()
            .HasDatabaseName("ux_platform_audit_logs__public_id");

        builder.HasIndex(log => new { log.ActorUserId, log.CreatedUtc })
            .HasDatabaseName("ix_platform_audit_logs__actor_created");

        builder.HasIndex(log => new { log.EntityType, log.EntityId })
            .HasDatabaseName("ix_platform_audit_logs__entity");

        builder.HasIndex(log => new { log.EventType, log.CreatedUtc })
            .HasDatabaseName("ix_platform_audit_logs__event_created");
    }
}
