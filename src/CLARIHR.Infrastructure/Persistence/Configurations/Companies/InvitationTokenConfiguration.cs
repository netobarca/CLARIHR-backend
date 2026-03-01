using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Companies;

internal sealed class InvitationTokenConfiguration : IEntityTypeConfiguration<InvitationToken>
{
    public void Configure(EntityTypeBuilder<InvitationToken> builder)
    {
        builder.ToTable("company_invitation_tokens");

        builder.HasKey(invitationToken => invitationToken.Id)
            .HasName("pk_company_invitation_tokens");

        builder.Property(invitationToken => invitationToken.Id)
            .HasColumnName("id");

        builder.Property(invitationToken => invitationToken.UserId)
            .HasColumnName("user_id");

        builder.Property(invitationToken => invitationToken.CompanyId)
            .HasColumnName("company_id");

        builder.Property(invitationToken => invitationToken.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(128);

        builder.Property(invitationToken => invitationToken.ExpirationUtc)
            .HasColumnName("expiration_utc");

        builder.Property(invitationToken => invitationToken.IsUsed)
            .HasColumnName("is_used");

        builder.Property(invitationToken => invitationToken.RevokedUtc)
            .HasColumnName("revoked_utc");

        builder.Property(invitationToken => invitationToken.CreatedUtc)
            .HasColumnName("created_utc");

        builder.Property(invitationToken => invitationToken.ModifiedUtc)
            .HasColumnName("modified_utc");

        builder.HasIndex(invitationToken => invitationToken.TokenHash)
            .IsUnique()
            .HasDatabaseName("uq_company_invitation_tokens__token_hash");

        builder.HasIndex(invitationToken => new { invitationToken.UserId, invitationToken.CompanyId })
            .HasDatabaseName("ix_company_invitation_tokens__user_company");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(invitationToken => invitationToken.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_invitation_tokens__auth_users");

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(invitationToken => invitationToken.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_company_invitation_tokens__companies");
    }
}
