using CLARIHR.Domain.Preferences;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CLARIHR.Infrastructure.Persistence.Configurations.Preferences;

internal sealed class UserSocialLinkConfiguration : IEntityTypeConfiguration<UserSocialLink>
{
    public void Configure(EntityTypeBuilder<UserSocialLink> builder)
    {
        builder.ToTable("user_social_links");

        builder.HasKey(socialLink => socialLink.Id)
            .HasName("pk_user_social_links");

        builder.Property(socialLink => socialLink.Id)
            .HasColumnName("id");

        builder.Property(socialLink => socialLink.PublicId)
            .HasColumnName("public_id");

        builder.Property(socialLink => socialLink.UserPreferenceId)
            .HasColumnName("user_preference_id");

        builder.Property(socialLink => socialLink.ProviderCode)
            .HasColumnName("provider_code")
            .HasMaxLength(50);

        builder.Property(socialLink => socialLink.Url)
            .HasColumnName("url")
            .HasMaxLength(500);

        builder.Property(socialLink => socialLink.SortOrder)
            .HasColumnName("sort_order");

        builder.HasIndex(socialLink => socialLink.PublicId)
            .IsUnique()
            .HasDatabaseName("uq_user_social_links__public_id");

        builder.HasIndex(socialLink => new { socialLink.UserPreferenceId, socialLink.ProviderCode })
            .IsUnique()
            .HasDatabaseName("uq_user_social_links__preference_provider");
    }
}
