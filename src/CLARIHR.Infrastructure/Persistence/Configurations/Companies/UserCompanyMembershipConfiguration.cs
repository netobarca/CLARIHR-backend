using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class UserCompanyMembershipConfiguration : IEntityTypeConfiguration<UserCompanyMembership>
{
    public void Configure(EntityTypeBuilder<UserCompanyMembership> builder)
    {
        builder.ToTable("user_companies");

        builder.HasKey(membership => membership.Id)
            .HasName("pk_user_companies");

        builder.Property(membership => membership.Id)
            .HasColumnName("id");

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id");

        builder.Property(membership => membership.CompanyId)
            .HasColumnName("company_id");

        builder.Property(membership => membership.RoleId)
            .HasColumnName("role_id");

        builder.Property(membership => membership.IsPrimary)
            .HasColumnName("is_primary");

        builder.Property(membership => membership.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(membership => membership.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(membership => membership.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(membership => membership.UserId)
            .IsUnique()
            .HasFilter("is_primary = true")
            .HasDatabaseName("uq_user_companies__primary_user");

        builder.HasIndex(membership => new { membership.UserId, membership.CompanyId })
            .IsUnique()
            .HasDatabaseName("uq_user_companies__user_company");

        builder.HasIndex(membership => new { membership.CompanyId, membership.Status, membership.RoleId })
            .HasDatabaseName("ix_user_companies__company_status_role");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_companies__auth_users");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(membership => membership.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_companies__companies");

        builder.HasOne<IamRole>()
            .WithMany()
            .HasForeignKey(membership => membership.RoleId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_user_companies__iam_roles");
    }
}
