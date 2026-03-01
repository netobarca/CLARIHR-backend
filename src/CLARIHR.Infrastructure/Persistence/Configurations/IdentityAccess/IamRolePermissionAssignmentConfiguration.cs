using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class IamRolePermissionAssignmentConfiguration : IEntityTypeConfiguration<IamRolePermissionAssignment>
{
    public void Configure(EntityTypeBuilder<IamRolePermissionAssignment> builder)
    {
        builder.ToTable("iam_role_permission_assignments");

        builder.HasKey(assignment => assignment.Id)
            .HasName("pk_iam_role_permission_assignments");

        builder.Property(assignment => assignment.Id)
            .HasColumnName("id");

        builder.Property(assignment => assignment.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(assignment => assignment.RoleId)
            .HasColumnName("role_id");

        builder.Property(assignment => assignment.PermissionId)
            .HasColumnName("permission_id");

        builder.Property(assignment => assignment.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(assignment => assignment.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(assignment => new { assignment.TenantId, assignment.RoleId, assignment.PermissionId })
            .IsUnique()
            .HasDatabaseName("uq_iam_role_perm__tenant_role_perm");
    }
}
