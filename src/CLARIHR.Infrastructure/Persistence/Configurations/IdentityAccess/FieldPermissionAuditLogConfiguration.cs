using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class FieldPermissionAuditLogConfiguration : IEntityTypeConfiguration<FieldPermissionAuditLog>
{
    public void Configure(EntityTypeBuilder<FieldPermissionAuditLog> builder)
    {
        builder.ToTable("field_permission_audit_logs");

        builder.HasKey(log => log.Id)
            .HasName("pk_field_permission_audit_logs");

        builder.Property(log => log.Id)
            .HasColumnName("id");

        builder.Property(log => log.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(log => log.RolePublicId)
            .HasColumnName("role_public_id");

        builder.Property(log => log.FieldKey)
            .HasColumnName("field_key")
            .HasMaxLength(150);

        builder.Property(log => log.NormalizedFieldKey)
            .HasColumnName("normalized_field_key")
            .HasMaxLength(150);

        builder.Property(log => log.ChangedByUserId)
            .HasColumnName("changed_by_user_id");

        builder.Property(log => log.BeforeJson)
            .HasColumnName("before_json")
            .HasColumnType("jsonb");

        builder.Property(log => log.AfterJson)
            .HasColumnName("after_json")
            .HasColumnType("jsonb");

        builder.Property(log => log.ChangedAtUtc)
            .HasColumnName("changed_at_utc");

        builder.Property(log => log.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(log => log.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(log => new { log.TenantId, log.RolePublicId, log.ChangedAtUtc })
            .HasDatabaseName("ix_field_permission_audit_logs__tenant_role_changed_at");

        builder.HasIndex(log => new { log.TenantId, log.NormalizedFieldKey, log.ChangedAtUtc })
            .HasDatabaseName("ix_field_permission_audit_logs__tenant_field_changed_at");
    }
}
