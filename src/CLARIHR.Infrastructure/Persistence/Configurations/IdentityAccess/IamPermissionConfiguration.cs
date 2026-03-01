using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class IamPermissionConfiguration : IEntityTypeConfiguration<IamPermission>
{
    public void Configure(EntityTypeBuilder<IamPermission> builder)
    {
        builder.ToTable("iam_permissions");

        builder.HasKey(permission => permission.Id)
            .HasName("pk_iam_permissions");

        builder.Property(permission => permission.Id)
            .HasColumnName("id");

        builder.Property(permission => permission.PublicId)
            .HasColumnName("public_id");

        builder.Property(permission => permission.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(permission => permission.Code)
            .HasColumnName("code")
            .HasMaxLength(200);

        builder.Property(permission => permission.NormalizedCode)
            .HasColumnName("normalized_code")
            .HasMaxLength(200);

        builder.Property(permission => permission.Name)
            .HasColumnName("name")
            .HasMaxLength(120);

        builder.Property(permission => permission.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(permission => permission.Module)
            .HasColumnName("module")
            .HasMaxLength(100);

        builder.Property(permission => permission.NormalizedModule)
            .HasColumnName("normalized_module")
            .HasMaxLength(100);

        builder.Property(permission => permission.Screen)
            .HasColumnName("screen")
            .HasMaxLength(100);

        builder.Property(permission => permission.NormalizedScreen)
            .HasColumnName("normalized_screen")
            .HasMaxLength(100);

        builder.Property(permission => permission.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(permission => permission.Action)
            .HasColumnName("action")
            .HasMaxLength(100);

        builder.Property(permission => permission.NormalizedAction)
            .HasColumnName("normalized_action")
            .HasMaxLength(100);

        builder.Property(permission => permission.FieldName)
            .HasColumnName("field_name")
            .HasMaxLength(100);

        builder.Property(permission => permission.NormalizedFieldName)
            .HasColumnName("normalized_field_name")
            .HasMaxLength(100);

        builder.Property(permission => permission.FieldAccess)
            .HasColumnName("field_access")
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(permission => permission.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(permission => permission.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(permission => new { permission.TenantId, permission.NormalizedCode })
            .IsUnique()
            .HasDatabaseName("uq_iam_permissions__tenant_code");

        builder.HasIndex(permission => new { permission.TenantId, permission.NormalizedModule, permission.NormalizedScreen })
            .HasDatabaseName("ix_iam_permissions__tenant_screen");

        builder.HasMany(permission => permission.RoleAssignments)
            .WithOne(assignment => assignment.Permission)
            .HasForeignKey(assignment => assignment.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(permission => permission.RoleAssignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
