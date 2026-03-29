using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.IdentityAccess;

internal sealed class IamUserConfiguration : IEntityTypeConfiguration<IamUser>
{
    public void Configure(EntityTypeBuilder<IamUser> builder)
    {
        builder.ToTable("iam_users");

        builder.HasKey(user => user.Id)
            .HasName("pk_iam_users");

        builder.Property(user => user.Id)
            .HasColumnName("id");

        builder.Property(user => user.PublicId)
            .HasColumnName("public_id");

        builder.Property(user => user.LinkedUserPublicId)
            .HasColumnName("linked_user_public_id");

        builder.Property(user => user.TenantId)
            .HasColumnName("tenant_id");

        builder.Property(user => user.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100);

        builder.Property(user => user.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100);

        builder.Property(user => user.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(user => user.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320);

        builder.Property(user => user.IsActive)
            .HasColumnName("is_active");

        builder.Property(user => user.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(user => user.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(user => new { user.TenantId, user.NormalizedEmail })
            .IsUnique()
            .HasDatabaseName("uq_iam_users__tenant_email");

        builder.HasIndex(user => new { user.TenantId, user.LinkedUserPublicId })
            .IsUnique()
            .HasDatabaseName("uq_iam_users__tenant_linked_user_public_id");

        builder.HasIndex(user => new { user.TenantId, user.LastName, user.FirstName })
            .HasDatabaseName("ix_iam_users__tenant_name");

        builder.HasMany(user => user.RoleAssignments)
            .WithOne(assignment => assignment.User)
            .HasForeignKey(assignment => assignment.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(user => user.RoleAssignments)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
