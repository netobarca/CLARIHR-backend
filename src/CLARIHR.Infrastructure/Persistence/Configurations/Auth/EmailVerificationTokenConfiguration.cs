using CLARIHR.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Auth;

internal sealed class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("auth_email_verification_tokens");

        builder.HasKey(emailVerificationToken => emailVerificationToken.Id)
            .HasName("pk_auth_email_verification_tokens");

        builder.Property(emailVerificationToken => emailVerificationToken.Id)
            .HasColumnName("id");

        builder.Property(emailVerificationToken => emailVerificationToken.PublicId)
            .HasColumnName("public_id");

        builder.Property(emailVerificationToken => emailVerificationToken.UserId)
            .HasColumnName("user_id");

        builder.Property(emailVerificationToken => emailVerificationToken.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128);

        builder.Property(emailVerificationToken => emailVerificationToken.ExpirationUtc)
            .HasColumnName("expiration_utc");

        builder.Property(emailVerificationToken => emailVerificationToken.IsUsed)
            .HasColumnName("is_used");

        builder.Property(emailVerificationToken => emailVerificationToken.UsedUtc)
            .HasColumnName("used_utc");

        builder.Property(emailVerificationToken => emailVerificationToken.RevokedUtc)
            .HasColumnName("revoked_utc");

        builder.Property(emailVerificationToken => emailVerificationToken.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(emailVerificationToken => emailVerificationToken.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(emailVerificationToken => emailVerificationToken.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_auth_email_verification_tokens__public_id");

        builder.HasIndex(emailVerificationToken => emailVerificationToken.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_auth_email_verification_tokens__token_hash");

        builder.HasIndex(emailVerificationToken => emailVerificationToken.UserId)
            .HasDatabaseName("ix_auth_email_verification_tokens__user_id");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(emailVerificationToken => emailVerificationToken.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_auth_email_verification_tokens__auth_users");
    }
}
