using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class RbacPermissionAuditLogConfiguration : IEntityTypeConfiguration<RbacPermissionAuditLog>
{
    public void Configure(EntityTypeBuilder<RbacPermissionAuditLog> builder)
    {
        builder.ToTable("rbac_permission_audit_logs");

        builder.HasKey(log => log.Id)
            .HasName("pk_rbac_permission_audit_logs");

        builder.Property(log => log.Id)
            .HasColumnName("id");

        builder.Property(log => log.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(log => log.RolePublicId)
            .HasColumnName("role_public_id");

        builder.Property(log => log.ResourceKey)
            .HasColumnName("resource_key")
            .HasMaxLength(100);

        builder.Property(log => log.NormalizedResourceKey)
            .HasColumnName("normalized_resource_key")
            .HasMaxLength(100);

        builder.Property(log => log.ChangedByUserId)
            .HasColumnName("changed_by_user_id");

        builder.Property(log => log.ChangeType)
            .HasColumnName("change_type")
            .HasConversion<string>()
            .HasMaxLength(20);

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
            .HasDatabaseName("ix_rbac_permission_audit_logs__tenant_role_changed_at");

        builder.HasIndex(log => new { log.TenantId, log.NormalizedResourceKey, log.ChangedAtUtc })
            .HasDatabaseName("ix_rbac_permission_audit_logs__tenant_resource_changed_at");
    }
}
