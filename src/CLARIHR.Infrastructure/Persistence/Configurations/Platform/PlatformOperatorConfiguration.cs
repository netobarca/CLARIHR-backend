using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Platform;

internal sealed class PlatformOperatorConfiguration : IEntityTypeConfiguration<PlatformOperator>
{
    public void Configure(EntityTypeBuilder<PlatformOperator> builder)
    {
        builder.ToTable("platform_operators");

        builder.HasKey(platformOperator => platformOperator.Id)
            .HasName("pk_platform_operators");

        builder.Property(platformOperator => platformOperator.Id)
            .HasColumnName("id");

        builder.Property(platformOperator => platformOperator.UserId)
            .HasColumnName("user_id");

        builder.Property(platformOperator => platformOperator.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(platformOperator => platformOperator.IsActive)
            .HasColumnName("is_active");

        builder.Property(platformOperator => platformOperator.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(platformOperator => platformOperator.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(platformOperator => platformOperator.UserId)
            .IsUnique()
            .HasDatabaseName("uq_platform_operators__user_id");

        builder.HasIndex(platformOperator => new { platformOperator.IsActive, platformOperator.Role })
            .HasDatabaseName("ix_platform_operators__active_role");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(platformOperator => platformOperator.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_platform_operators__auth_users");
    }
}
