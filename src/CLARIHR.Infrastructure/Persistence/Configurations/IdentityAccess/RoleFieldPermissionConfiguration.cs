using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class RoleFieldPermissionConfiguration : IEntityTypeConfiguration<RoleFieldPermission>
{
    public void Configure(EntityTypeBuilder<RoleFieldPermission> builder)
    {
        builder.ToTable("role_field_permissions");

        builder.HasKey(permission => permission.Id)
            .HasName("pk_role_field_permissions");

        builder.Property(permission => permission.Id)
            .HasColumnName("id");

        builder.Property(permission => permission.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(permission => permission.RoleId)
            .HasColumnName("role_id");

        builder.Property(permission => permission.FieldKey)
            .HasColumnName("field_key")
            .HasMaxLength(150);

        builder.Property(permission => permission.NormalizedFieldKey)
            .HasColumnName("normalized_field_key")
            .HasMaxLength(150);

        builder.Property(permission => permission.IsVisible)
            .HasColumnName("is_visible");

        builder.Property(permission => permission.IsEditable)
            .HasColumnName("is_editable");

        builder.Property(permission => permission.IsRequired)
            .HasColumnName("is_required");

        builder.Property(permission => permission.IsMasked)
            .HasColumnName("is_masked");

        builder.Property(permission => permission.UpdatedByUserId)
            .HasColumnName("updated_by_user_id");

        builder.Property(permission => permission.UpdatedAtUtc)
            .HasColumnName("updated_at_utc");

        builder.Property(permission => permission.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(permission => permission.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(permission => new { permission.TenantId, permission.RoleId, permission.NormalizedFieldKey })
            .IsUnique()
            .HasDatabaseName("uq_role_field_permissions__tenant_role_field");

        builder.HasIndex(permission => new { permission.TenantId, permission.RoleId })
            .HasDatabaseName("ix_role_field_permissions__tenant_role");
    }
}
