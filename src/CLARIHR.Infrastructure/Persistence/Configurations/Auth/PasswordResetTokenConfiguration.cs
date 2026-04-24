using CLARIHR.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Auth;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("auth_password_reset_tokens");

        builder.HasKey(passwordResetToken => passwordResetToken.Id)
            .HasName("pk_auth_password_reset_tokens");

        builder.Property(passwordResetToken => passwordResetToken.Id)
            .HasColumnName("id");

        builder.Property(passwordResetToken => passwordResetToken.PublicId)
            .HasColumnName("public_id");

        builder.Property(passwordResetToken => passwordResetToken.UserId)
            .HasColumnName("user_id");

        builder.Property(passwordResetToken => passwordResetToken.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128);

        builder.Property(passwordResetToken => passwordResetToken.ExpirationUtc)
            .HasColumnName("expiration_utc");

        builder.Property(passwordResetToken => passwordResetToken.IsUsed)
            .HasColumnName("is_used");

        builder.Property(passwordResetToken => passwordResetToken.UsedUtc)
            .HasColumnName("used_utc");

        builder.Property(passwordResetToken => passwordResetToken.RevokedUtc)
            .HasColumnName("revoked_utc");

        builder.Property(passwordResetToken => passwordResetToken.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(passwordResetToken => passwordResetToken.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(passwordResetToken => passwordResetToken.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_auth_password_reset_tokens__public_id");

        builder.HasIndex(passwordResetToken => passwordResetToken.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_auth_password_reset_tokens__token_hash");

        builder.HasIndex(passwordResetToken => passwordResetToken.UserId)
            .HasDatabaseName("ix_auth_password_reset_tokens__user_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(passwordResetToken => passwordResetToken.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_auth_password_reset_tokens__auth_users");
    }
}
