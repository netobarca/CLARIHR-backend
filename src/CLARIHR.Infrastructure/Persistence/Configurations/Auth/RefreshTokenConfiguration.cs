using CLARIHR.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Auth;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("auth_refresh_tokens");

        builder.HasKey(refreshToken => refreshToken.Id)
            .HasName("pk_auth_refresh_tokens");

        builder.Property(refreshToken => refreshToken.Id)
            .HasColumnName("id");

        builder.Property(refreshToken => refreshToken.FamilyId)
            .HasColumnName("family_id");

        builder.Property(refreshToken => refreshToken.UserId)
            .HasColumnName("user_id");

        builder.Property(refreshToken => refreshToken.ClientType)
            .HasColumnName("client_type")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(refreshToken => refreshToken.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128);

        builder.Property(refreshToken => refreshToken.ExpiresUtc)
            .HasColumnName("expires_utc");

        builder.Property(refreshToken => refreshToken.RevokedUtc)
            .HasColumnName("revoked_utc");

        builder.Property(refreshToken => refreshToken.ReplacedByTokenHash)
            .HasColumnName("replaced_by_token_hash")
            .HasMaxLength(128);

        builder.Property(refreshToken => refreshToken.RevocationReason)
            .HasColumnName("revocation_reason")
            .HasMaxLength(100);

        builder.Property(refreshToken => refreshToken.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(refreshToken => refreshToken.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(refreshToken => refreshToken.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_auth_refresh_tokens__token_hash");

        builder.HasIndex(refreshToken => new { refreshToken.FamilyId, refreshToken.UserId })
            .HasDatabaseName("ix_auth_refresh_tokens__family_user");

        builder.HasIndex(refreshToken => new { refreshToken.UserId, refreshToken.ClientType, refreshToken.RevokedUtc })
            .HasDatabaseName("ix_auth_refresh_tokens__user_client_revoked");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(refreshToken => refreshToken.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_auth_refresh_tokens__auth_users");
    }
}
