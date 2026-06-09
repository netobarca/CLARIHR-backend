using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class IamRoleConfiguration : IEntityTypeConfiguration<IamRole>
{
    public void Configure(EntityTypeBuilder<IamRole> builder)
    {
        builder.ToTable("iam_roles");

        builder.HasKey(role => role.Id)
            .HasName("pk_iam_roles");

        builder.Property(role => role.Id)
            .HasColumnName("id");

        builder.Property(role => role.PublicId)
            .HasColumnName("public_id");

        builder.Property(role => role.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(role => role.Name)
            .HasColumnName("name")
            .HasMaxLength(100);

        builder.Property(role => role.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(100);

        builder.Property(role => role.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(role => role.IsSystemRole)
            .HasColumnName("is_system_role");

        builder.Property(role => role.ConcurrencyToken)
            .HasColumnName("concurrency_token")
            .IsConcurrencyToken();

        builder.Property(role => role.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(role => role.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(role => new { role.TenantId, role.NormalizedName })
            .IsUnique()
            .HasDatabaseName("uq_iam_roles__tenant_name");

        builder.HasIndex(role => new { role.TenantId, role.Name })
            .HasDatabaseName("ix_iam_roles__tenant_name");

        builder.HasMany(role => role.PermissionAssignments)
            .WithOne(assignment => assignment.Role)
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(role => role.UserAssignments)
            .WithOne(assignment => assignment.Role)
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(role => role.PermissionAssignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(role => role.UserAssignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
