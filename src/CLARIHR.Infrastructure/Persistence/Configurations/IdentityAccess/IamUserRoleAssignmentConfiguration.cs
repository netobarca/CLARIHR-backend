using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class IamUserRoleAssignmentConfiguration : IEntityTypeConfiguration<IamUserRoleAssignment>
{
    public void Configure(EntityTypeBuilder<IamUserRoleAssignment> builder)
    {
        builder.ToTable("iam_user_role_assignments");

        builder.HasKey(assignment => assignment.Id)
            .HasName("pk_iam_user_role_assignments");

        builder.Property(assignment => assignment.Id)
            .HasColumnName("id");

        builder.Property(assignment => assignment.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(assignment => assignment.UserId)
            .HasColumnName("user_id");

        builder.Property(assignment => assignment.RoleId)
            .HasColumnName("role_id");

        builder.Property(assignment => assignment.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(assignment => assignment.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(assignment => new { assignment.TenantId, assignment.UserId, assignment.RoleId })
            .IsUnique()
            .HasDatabaseName("uq_iam_user_role__tenant_user_role");
    }
}
